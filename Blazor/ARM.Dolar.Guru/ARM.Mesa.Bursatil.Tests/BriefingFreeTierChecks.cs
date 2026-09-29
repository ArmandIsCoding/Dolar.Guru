using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;
using ARM.Mesa.Bursatil.Sync;
using Microsoft.Extensions.Configuration;

internal static class BriefingFreeTierChecks
{
    internal static BriefingOptions Options() => new()
    {
        Enabled = true, Provider = "Gemini", Model = "gemini-3.8-flash",
        BillingMode = "FreeTier", FreeTierConfirmed = true, MonthlyBudgetUsd = 0
    };

    public static async Task RunAsync(string temporary)
    {
        var options = Options();
        options.Validate();
        Check(true, "Gemini FreeTier accepts zero pricing with explicit confirmation");
        options.FreeTierConfirmed = false;
        Throws(options.Validate, "FreeTier fails closed without account confirmation");
        options.FreeTierConfirmed = true; options.Provider = "OpenAI";
        Throws(options.Validate, "FreeTier cannot silently use another provider");
        options.Provider = "Gemini"; options.BillingMode = "FreeTire";
        Throws(options.Validate, "Unknown billing mode rejected");
        options.BillingMode = "Paid";
        Throws(options.Validate, "Paid mode still requires positive budget and prices");
        options = Options(); options.MonthlyBudgetUsd = 10;
        Throws(options.Validate, "FreeTier requires explicit zero budget, not inherited paid budget");
        options = Options(); options.InputUsdPerMillion = 1;
        Throws(options.Validate, "FreeTier rejects inconsistent positive rates");
        options = Options(); options.MaxRequestsPer24Hours = 0;
        Throws(options.Validate, "Zero request cap cannot mean unlimited requests");
        options = Options(); options.MaxRequestsPerMonth = 125;
        Throws(options.Validate, "Monthly request cap has a conservative upper bound");
        options = Options(); options.MaxInputTokensPerRequest = 250001;
        Throws(options.Validate, "Input token cap cannot be unbounded");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Briefing:Provider"] = "Gemini", ["Briefing:Model"] = "gemini-3.8-flash",
            ["Briefing:BillingMode"] = "FreeTier", ["Briefing:FreeTierConfirmed"] = "true",
            ["Briefing:MonthlyBudgetUsd"] = "0", ["Briefing:MaxRequestsPer24Hours"] = "2",
            ["Briefing:MaxRequestsPerMonth"] = "30", ["Briefing:MaxInputTokensPerRequest"] = "50000"
        }).Build();
        var configured = BriefingCommands.ReadOptions(config);
        configured.Validate();
        Check(configured.IsFreeTier && configured.MaxRequestsPer24Hours == 2
            && configured.MaxRequestsPerMonth == 30 && configured.MaxInputTokensPerRequest == 50000,
            "FreeTier and all caps load from appsettings configuration without environment variables");
        Throws(() => BriefingCommands.CheckConfiguration(config, _ => { }), "Local check requires a private API key");
        config["Briefing:ApiKey"] = "synthetic-check-secret";
        var checkMessages = new List<string>();
        BriefingCommands.CheckConfiguration(config, checkMessages.Add);
        Check(checkMessages.Any(m => m.Contains("Sin llamadas a la API"))
            && !checkMessages.Any(m => m.Contains("synthetic-check-secret")),
            "Local configuration check works while disabled and never prints credentials");

        var now = new DateTime(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
        var db = new MarketDatabase(Path.Combine(temporary, "free-tier.db")); db.Initialize();
        var service = new BriefingService(db);
        for (var i = 0; i < 4; i++)
        {
            Check(Reserve(service, "slot" + i, now.AddHours(i * 4)), "Free request reserved atomically " + i);
            service.FailRun("slot" + i);
        }
        Check(!Reserve(new BriefingService(db), "blocked", now.AddHours(16)),
            "Failures and restarts retain the rolling 24h quota");
        Check(!Reserve(service, "blocked-midnight", now.AddHours(23)), "Argentine midnight does not reset the rolling cap");
        Check(Reserve(service, "next-day", now.AddHours(24)), "Old request expires at the rolling 24h boundary");
        using (var connection = db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM BriefingRuns WHERE BillingMode='FreeTier' AND ReservedUsd=0";
            Check((long)command.ExecuteScalar()! == 5, "Free ledger records mode and zero local reservation, without fictitious tariffs");
        }

        var monthlyDb = new MarketDatabase(Path.Combine(temporary, "free-monthly.db")); monthlyDb.Initialize();
        var monthly = new BriefingService(monthlyDb);
        var monthEnd = new DateTime(2026, 9, 30, 23, 50, 0, DateTimeKind.Utc);
        Check(Reserve(monthly, "last-month", monthEnd, monthlyCap: 1), "Free monthly reservation allowed");
        Check(!Reserve(monthly, "month-blocked", monthEnd.AddMinutes(2), monthlyCap: 1), "Free monthly cap blocks additional requests");
        Check(Reserve(monthly, "new-month", monthEnd.AddMinutes(20), monthlyCap: 1), "Request month resets in UTC");
        Check(!Reserve(monthly, "minute-blocked", monthEnd.AddMinutes(20).AddSeconds(20)), "Cross-slot requests cannot burst within one minute");
        Check(!Reserve(monthly, "day-blocked", monthEnd.AddMinutes(22), dailyCap: 2), "Month reset does not reset rolling 24h usage");
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(() =>
            Reserve(new BriefingService(monthlyDb), "race-" + i, monthEnd.AddDays(2), dailyCap: 1))));
        Check(outcomes.Count(x => x) == 1, "Concurrent distinct slots cannot oversubscribe the free request cap");

        var legacyDb = new MarketDatabase(Path.Combine(temporary, "free-migration.db"));
        using (var connection = legacyDb.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE BriefingRuns (Slot TEXT PRIMARY KEY, Fingerprint TEXT NOT NULL, StartedAtUtc TEXT NOT NULL,
                Month TEXT NOT NULL, Status TEXT NOT NULL, ReservedUsd REAL NOT NULL, ChargedUsd REAL,
                InputTokens INTEGER, OutputTokens INTEGER, Provider TEXT NOT NULL, Model TEXT NOT NULL, EditionId INTEGER, Error TEXT);
                INSERT INTO BriefingRuns VALUES ('legacy','legacy','2026-09-28T15:00:00Z','2026-09','failed',1,NULL,NULL,NULL,'OpenAI','test',NULL,NULL);
                """;
            command.ExecuteNonQuery();
        }
        legacyDb.Initialize(); legacyDb.Initialize();
        using (var connection = legacyDb.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT BillingMode FROM BriefingRuns WHERE Slot='legacy' AND ReservedUsd=1";
            Check((string?)command.ExecuteScalar() == "Paid", "Additive repeated migration preserves existing paid reservations");
        }
        Check(!Reserve(new BriefingService(legacyDb), "switch-mode", now.AddMinutes(2), dailyCap: 1),
            "Changing billing mode cannot reset request counts");

        var generationDb = new MarketDatabase(Path.Combine(temporary, "free-generation.db")); generationDb.Initialize();
        BriefingChecks.SeedNews(generationDb, now);
        var published = new BriefingService(generationDb);
        var publishedId = published.SaveDraft(BriefingChecks.Fixture(now)); published.Publish(publishedId, "Test", now);
        var fake = new FakeFreeAi();
        options = Options(); options.MaxInputTokensPerRequest = 1000;
        Check(await new BriefingGenerator(generationDb, options, fake).GenerateAsync(now) is null && fake.Calls == 0,
            "Oversized input is blocked before reservation or provider call");
        options.MaxInputTokensPerRequest = 200000;
        var messages = new List<string>();
        var generator = new BriefingGenerator(generationDb, options, fake, messages.Add);
        var id = await generator.GenerateAsync(now);
        Check(id > 0 && fake.Calls == 1 && published.GetPublished(id.Value) is null
            && published.GetLatestPublished()!.Id == publishedId && messages.Any(m => m.Contains("FreeTier declarado")),
            "Free generation saves a reviewed-later draft and preserves previous publication");
        Check(await generator.GenerateAsync(now.AddMinutes(1)) is null && fake.Calls == 1,
            "Free mode does not repeat the same slot");
        Check(await generator.GenerateAsync(now.AddHours(4)) is null && fake.Calls == 1,
            "Free mode does not regenerate unchanged evidence");

        var failureDb = new MarketDatabase(Path.Combine(temporary, "free-quota-failure.db")); failureDb.Initialize();
        BriefingChecks.SeedNews(failureDb, now);
        var failureService = new BriefingService(failureDb);
        var oldId = failureService.SaveDraft(BriefingChecks.Fixture(now)); failureService.Publish(oldId, "Test", now);
        var failing = new FakeFreeAi { Fail = true };
        try { await new BriefingGenerator(failureDb, Options(), failing).GenerateAsync(now); throw new Exception("Expected 429"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("429")) { }
        Check(await new BriefingGenerator(failureDb, Options(), failing).GenerateAsync(now.AddMinutes(5)) is null
            && failing.Calls == 1 && failureService.GetLatestPublished()!.Id == oldId && failureService.ListDrafts().Count == 0,
            "Exhausted provider quota retains last publication and does not retry the failed slot");
    }

    private static bool Reserve(BriefingService service, string slot, DateTime now, int dailyCap = 4, int monthlyCap = 124) =>
        service.TryReserve(slot, slot, now, 0, 0, "Gemini", "test", out _, true, dailyCap, monthlyCap);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Check(true, message); return; }
        throw new InvalidOperationException("FAIL: " + message);
    }

    private sealed class FakeFreeAi : IBriefingAiClient
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public int EstimateMaxInputTokens(IReadOnlyList<BriefingSource> sources, DateTime coverageEndUtc) => 10000;
        public int EstimateMaxOutputTokens() => 5000;
        public Task<BriefingAiResult> GenerateAsync(IReadOnlyList<BriefingSource> sources, DateTime coverageEndUtc, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail) throw new BriefingQuotaExceededException();
            var fixture = BriefingChecks.Fixture(coverageEndUtc);
            return Task.FromResult(new BriefingAiResult(fixture.Summary, fixture.Topics, 1000, 500));
        }
    }
}
