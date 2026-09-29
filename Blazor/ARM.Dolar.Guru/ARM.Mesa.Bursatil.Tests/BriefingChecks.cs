using System.Text.Json;
using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;
using ARM.Mesa.Bursatil.Sync;
using Microsoft.Extensions.Configuration;

internal static class BriefingChecks
{
    public static async Task RunAsync(string temporary)
    {
        var now = new DateTime(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
        var db = new MarketDatabase(Path.Combine(temporary, "briefing.db"));
        db.Initialize();
        db.Initialize();
        var service = new BriefingService(db);
        Check(service.GetLatestPublished() is null, "Fresh schema exposes no invented edition");
        var edition = Fixture(now);
        var id = service.SaveDraft(edition);
        Check(id > 0 && service.GetLatestPublished() is null && service.GetPublished(id) is null
            && service.ListDrafts().Count == 1, "Drafts are private until reviewed");
        Throws(() => service.Publish(id, "", now), "Publishing requires reviewer identity");
        Throws(() => BriefingCommands.HandleReview(["--briefing-publish", id.ToString()], db), "CLI requires explicit human-review confirmation");
        service.Publish(id, "Revisor de prueba", now);
        Check(service.GetLatestPublished() is { Status: "published", ReviewedBy: "Revisor de prueba" }, "Human approval publishes frozen evidence");
        Throws(() => service.Publish(id, "Revisor", now), "Publishing is not repeatable");
        var draftId = service.SaveDraft(Fixture(now.AddMinutes(1)));
        Check(service.GetLatestPublished()!.Id == id, "New pending edition does not replace published edition");
        service.Reject(draftId);
        Check(service.GetPublished(draftId) is null && service.GetLatestPublished()!.Id == id, "Rejected edition stays private and preserves last publication");
        var expiredId = service.SaveDraft(Fixture(now.AddDays(-3)));
        Throws(() => service.Publish(expiredId, "Revisor", now), "Stale draft cannot be published as current news");

        var bad = Fixture(now); bad.Topics[0].WhatHappened.Citations[0].NewsId = 999;
        Throws(() => service.SaveDraft(bad), "Unknown citation IDs rejected");
        bad = Fixture(now); bad.Summary[0].Citations[0].Evidence = "Esta afirmación jamás apareció en la fuente citada.";
        Throws(() => service.SaveDraft(bad), "Fabricated supporting excerpts rejected");
        bad = Fixture(now); bad.Sources[0].Url = "javascript:alert(1)";
        Throws(() => service.SaveDraft(bad), "Unsafe source links rejected");
        bad = Fixture(now); bad.Sources.ForEach(s => s.PublisherKey = "unico-grupo");
        Throws(() => service.SaveDraft(bad), "Multiple outlets of same publisher do not imply diversity");
        bad = Fixture(now); bad.Topics[0].IsInternational = bad.Topics[1].IsInternational = true;
        Throws(() => service.SaveDraft(bad), "International coverage limited to one topic");
        bad = Fixture(now); bad.Summary[0].Citations.Clear();
        Throws(() => service.SaveDraft(bad), "Uncited summary rejected");
        Check(service.GetLatestPublished()!.Id == id, "Invalid outputs never replace a published edition");

        Check(BriefingGenerator.GetSlot(now.AddHours(-5), [8, 12, 16, 20]) is null
            && BriefingGenerator.GetSlot(now, [8, 12, 16, 20]) == "2026-09-28/12", "Editorial slots use Argentine clock");
        var options = new BriefingOptions { Enabled = false };
        var fake = new FakeAi();
        Check(await new BriefingGenerator(db, options, fake).GenerateAsync(now) is null && fake.Calls == 0,
            "Disabled synthesis never calls provider or requires a secret");
        options.Enabled = true; options.Model = "test-model"; options.InputUsdPerMillion = 1; options.OutputUsdPerMillion = 2;
        Check(await new BriefingGenerator(db, options, fake).GenerateAsync(now) is null && fake.Calls == 0,
            "Insufficient authorized evidence skips API call");
        SeedNews(db, now);
        var generator = new BriefingGenerator(db, options, fake);
        var generatedId = await generator.GenerateAsync(now);
        Check(generatedId > 0 && fake.Calls == 1 && service.GetPublished(generatedId.Value) is null,
            "Generation stores valid cited draft, never auto-publishes");
        Check(await generator.GenerateAsync(now.AddMinutes(1)) is null && fake.Calls == 1,
            "Repeated sync in same slot does not spend again");
        Check(await generator.GenerateAsync(now.AddHours(4)) is null && fake.Calls == 1,
            "Unchanged evidence does not regenerate next slot");

        var ledger = new MarketDatabase(Path.Combine(temporary, "budget.db")); ledger.Initialize();
        var budget = new BriefingService(ledger);
        Check(budget.TryReserve("slot1", "hash1", now, 7m, 10m, "test", "test", out _), "Cost reserved before request");
        budget.FailRun("slot1");
        Check(!budget.TryReserve("slot2", "hash2", now.AddMinutes(2), 4m, 10m, "test", "test", out var budgetReason)
            && budgetReason.Contains("presupuesto mensual"), "Timeout or failure retains reservation against monthly limit");
        Check(!budget.TryReserve("slot1", "hash1", now, 1m, 10m, "test", "test", out _), "Failed slot cannot accidentally retry and double-charge");
        Check(budget.TryReserve("nextmonth", "hash2", now.AddMonths(1), 4m, 10m, "test", "test", out _), "New month has separate budget");
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(index => Task.Run(() =>
            new BriefingService(ledger).TryReserve("concurrent", "concurrent-hash", now.AddMonths(1).AddMinutes(2), 2m, 10m, "test", "test", out _))));
        Check(outcomes.Count(x => x) == 1, "Concurrent sync processes reserve a slot exactly once");
        Check(!budget.TryReserve("different-slot", "concurrent-hash", now.AddMonths(1), 1m, 10m, "test", "test", out _),
            "Identical evidence still in flight is blocked across different editorial slots");
        var invalidHours = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Briefing:ScheduleHours:0"] = "-1", ["Briefing:Model"] = "test-model",
            ["Briefing:InputUsdPerMillion"] = "1", ["Briefing:OutputUsdPerMillion"] = "2"
        }).Build();
        Throws(() => BriefingCommands.ReadOptions(invalidHours).Validate(), "Invalid schedule never silently enables four billable slots");

        var failureDb = new MarketDatabase(Path.Combine(temporary, "generation-failure.db")); failureDb.Initialize();
        SeedNews(failureDb, now);
        var failing = new FakeAi { Fail = true };
        try { await new BriefingGenerator(failureDb, options, failing).GenerateAsync(now); throw new Exception("Expected failure"); }
        catch (InvalidDataException) { }
        Check(await new BriefingGenerator(failureDb, options, failing).GenerateAsync(now) is null && failing.Calls == 1
            && new BriefingService(failureDb).GetLatestPublished() is null, "Failure leaves no publication and cannot retry slot");
    }

    internal static void SeedNews(MarketDatabase database, DateTime now)
    {
        foreach (var source in Fixture(now).Sources)
        {
            using var connection = database.Open(); using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO News(Id,Titulo,Resumen,Url,Fuente,FechaPublicacion,SourceId,PublisherGroup,IsInternational,AllowAiUse,FetchedAtUtc,ContentHash,ContentText,ContentKind)
                VALUES($id,$title,$text,$url,$name,$date,$source,$publisher,0,1,$date,$source,$text,'rss-summary')
                """;
            command.Parameters.AddWithValue("$id", source.NewsId); command.Parameters.AddWithValue("$title", source.Title);
            command.Parameters.AddWithValue("$text", source.EvidenceText); command.Parameters.AddWithValue("$url", source.Url);
            command.Parameters.AddWithValue("$name", source.SourceName); command.Parameters.AddWithValue("$date", source.PublishedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$source", "test-" + source.NewsId); command.Parameters.AddWithValue("$publisher", source.PublisherKey);
            command.ExecuteNonQuery();
        }
    }

    public static NewsBriefing Fixture(DateTime now)
    {
        var sources = Enumerable.Range(1, 4).Select(i => new BriefingSource
        {
            NewsId = i, Title = "Noticia sintética de prueba " + i, SourceName = "Fuente de prueba " + i,
            PublisherKey = "grupo-" + i, Url = "https://example.com/noticia/" + i,
            EvidenceText = $"En este escenario ficticio de pruebas, la fuente {i} informa un acontecimiento verificable dentro del conjunto de datos simulado. No representa una noticia real ni debe publicarse en producción.",
            PublishedAtUtc = now.AddHours(-1)
        }).ToList();
        BriefingStatement Statement(BriefingSource s) => new()
        {
            Text = "Contenido sintético de prueba, no es información de actualidad.",
            Citations = [new() { NewsId = s.NewsId, Evidence = s.EvidenceText[..100] }]
        };
        return new NewsBriefing
        {
            CreatedAtUtc = now, CoverageStartUtc = now.AddHours(-1), CoverageEndUtc = now,
            Model = "test", Provider = "test", Sources = sources,
            Summary = [Statement(sources[0]), Statement(sources[1])],
            Topics = sources.Select(s => new BriefingTopic
            {
                Title = s.Title, WhatHappened = Statement(s), WhyItMatters = Statement(s), WhatToWatch = Statement(s)
            }).ToList()
        };
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    private static void Throws(Action action, string message)
    {
        var threw = false;
        try { action(); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException) { threw = true; }
        Check(threw, message);
    }
    private sealed class FakeAi : IBriefingAiClient
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public int EstimateMaxInputTokens(IReadOnlyList<BriefingSource> sources, DateTime coverageEndUtc) => 10000;
        public int EstimateMaxOutputTokens() => 5000;
        public Task<BriefingAiResult> GenerateAsync(IReadOnlyList<BriefingSource> sources, DateTime coverageEndUtc, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail) throw new InvalidDataException("Simulated provider failure");
            var edition = Fixture(coverageEndUtc);
            return Task.FromResult(new BriefingAiResult(edition.Summary, edition.Topics, 1000, 500));
        }
    }
}
