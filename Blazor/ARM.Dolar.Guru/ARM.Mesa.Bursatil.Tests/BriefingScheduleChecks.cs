using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;
using ARM.Mesa.Bursatil.Sync;
using Microsoft.Extensions.Configuration;

internal static class BriefingScheduleChecks
{
    public static async Task RunAsync(string temporary)
    {
        var now = new DateTime(2026, 9, 29, 9, 0, 0, DateTimeKind.Utc); // 06:00 Argentina
        var db = new MarketDatabase(Path.Combine(temporary, "automatic-schedule.db")); db.Initialize();
        var options = BriefingFreeTierChecks.Options();
        var ai = new FakeAi();
        var service = new BriefingService(db);
        Check(!service.HasAnyEdition(), "Empty briefing store detected independently of cached market/news data");
        Check(await new BriefingGenerator(db, options, ai).GenerateAsync(now) is null && ai.Calls == 0,
            "Empty database does not send an evidence-free prompt to AI");
        BriefingChecks.SeedNews(db, now);
        options.GenerateWhenEmpty = false;
        Check(await new BriefingGenerator(db, options, ai).GenerateAsync(now) is null && ai.Calls == 0,
            "GenerateWhenEmpty=false respects first scheduled hour even on initial setup");
        options.GenerateWhenEmpty = true;
        options.Enabled = false;
        Check(await new BriefingGenerator(db, options, ai).GenerateAsync(now) is null && ai.Calls == 0,
            "Empty database never overrides disabled generation");
        options.Enabled = true;
        var id = await new BriefingGenerator(db, options, ai).GenerateAsync(now);
        Check(id > 0 && ai.Calls == 1 && service.HasAnyEdition() && service.GetLatestPublished() is null,
            "First edition is generated before the schedule and saved as a draft");
        ChangeHeadline(db);
        Check(await new BriefingGenerator(db, options, ai).GenerateAsync(now.AddMinutes(5)) is null && ai.Calls == 1,
            "An unpublished draft prevents bootstrap calls every five minutes, even with changed news");
        Check(await new BriefingGenerator(db, options, ai).GenerateAsync(now.AddHours(2)) is null && ai.Calls == 1,
            "Bootstrap consumes today's first scheduled slot rather than adding an extra call");
        Check(await new BriefingGenerator(db, options, ai).GenerateAsync(now.AddHours(6)) > 0 && ai.Calls == 2,
            "Next scheduled slot can generate when evidence changes");

        var failureDb = new MarketDatabase(Path.Combine(temporary, "bootstrap-failure.db")); failureDb.Initialize();
        BriefingChecks.SeedNews(failureDb, now);
        var failing = new FakeAi { Fail = true };
        try { await new BriefingGenerator(failureDb, options, failing).GenerateAsync(now); throw new Exception("Expected failure"); }
        catch (BriefingQuotaExceededException) { }
        Check(await new BriefingGenerator(failureDb, options, failing).GenerateAsync(now.AddMinutes(5)) is null
            && await new BriefingGenerator(failureDb, options, failing).GenerateAsync(now.AddHours(2)) is null
            && failing.Calls == 1, "Failed bootstrap is not retried every five minutes or at the first scheduled hour");

        var cappedDb = new MarketDatabase(Path.Combine(temporary, "bootstrap-cap.db")); cappedDb.Initialize();
        BriefingChecks.SeedNews(cappedDb, now);
        var cappedAi = new FakeAi();
        var cappedOptions = BriefingFreeTierChecks.Options(); cappedOptions.MaxRequestsPer24Hours = 1;
        await new BriefingGenerator(cappedDb, cappedOptions, cappedAi).GenerateAsync(now);
        ChangeHeadline(cappedDb);
        Check(await new BriefingGenerator(cappedDb, cappedOptions, cappedAi).GenerateAsync(now.AddHours(6)) is null && cappedAi.Calls == 1,
            "Bootstrap counts against persistent rolling request limits");
        var rejected = new BriefingService(cappedDb); rejected.Reject(rejected.ListDrafts().Single().Id);
        Check(rejected.HasAnyEdition(), "Rejected editions count as history, not as an empty database");

        var directory = Path.Combine(temporary, "two-editions-config"); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), """{"Briefing":{"ScheduleHours":[8,12,16,20]}}""");
        await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.Production.json"),
            """{"Briefing":{"ScheduleHours":[8,20],"GenerateWhenEmpty":false}}""");
        var config = SyncConfiguration.Load(directory);
        using (config as IDisposable)
        {
            var loaded = BriefingCommands.ReadOptions(config);
            Check(loaded.ScheduleHours.SequenceEqual(new[] { 8, 20 }) && !loaded.GenerateWhenEmpty,
                "Private two-edition schedule replaces all four base hours instead of merging array indices");
            Check(BriefingGenerator.GetSlot(now.AddHours(10), loaded.ScheduleHours) == "2026-09-29/08"
                && BriefingGenerator.GetSlot(now.AddHours(14), loaded.ScheduleHours) == "2026-09-29/20",
                "Two-edition configuration has no noon or afternoon generation slots");
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.Production.json"), """{"Briefing":{"ScheduleHours":[]}}""");
        config = SyncConfiguration.Load(directory);
        using (config as IDisposable)
        {
            try { BriefingCommands.ReadOptions(config); throw new Exception("Empty schedule should fail"); }
            catch (ArgumentException) { Check(true, "Explicit empty schedule cannot fall back to four calls silently"); }
        }

        using var log = new ExecutionLog(Path.Combine(temporary, "automatic-sync-logs"));
        var localConfig = new ConfigurationBuilder().Build();
        Check(await SyncMaintenance.TryRunAsync([], db, localConfig, log) is null
            && SyncMaintenance.ReadPath([], "--settings") is null && SyncMaintenance.ReadPath([], "--database") is null,
            "No arguments selects automatic sync, with no maintenance or path overrides");
        using var http = new HttpClient(new MarketStubHttp());
        var called = false;
        var result = await SyncWorkflow.RunAsync(new MarketSynchronizer(db, http, log), async () =>
        {
            Check((await new CotizacionesService(db).ObtenerUltimasCotizacionesAsync()).Item1.Count > 0,
                "Automatic workflow refreshes market data before considering AI");
            called = true;
            throw new BriefingQuotaExceededException();
        }, log);
        Check(called && result == 1 && (await new CotizacionesService(db).ObtenerUltimasCotizacionesAsync()).Item1.Count > 0,
            "Normal workflow reports AI failure without undoing fresh prices");
    }

    private static void ChangeHeadline(MarketDatabase db)
    {
        using var connection = db.Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE News SET Titulo=Titulo || ' actualización' WHERE Id=1";
        command.ExecuteNonQuery();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
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
            if (Fail) throw new BriefingQuotaExceededException();
            var fixture = BriefingChecks.Fixture(coverageEndUtc);
            return Task.FromResult(new BriefingAiResult(fixture.Summary, fixture.Topics, 1000, 500));
        }
    }
}
