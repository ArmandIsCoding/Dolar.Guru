using ARM.Mesa.Bursatil.Sync;

internal static class SyncConfigurationChecks
{
    public static async Task RunAsync(string temporary)
    {
        var directory = Path.Combine(temporary, "settings");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"),
            """{"Briefing":{"Enabled":false,"Provider":"OpenAI","MonthlyBudgetUsd":10},"Database":{"Path":"base.db"}}""");
        var baseOnly = SyncConfiguration.Load(directory);
        using (baseOnly as IDisposable)
        {
            Check(baseOnly["Briefing:Enabled"] == "False", "Optional private settings not required while AI disabled");
            Throws(() => SyncConfiguration.ReadApiKey(baseOnly), "Missing configured API key fails closed");
        }
        var privateFile = Path.Combine(directory, "appsettings.Production.json");
        await File.WriteAllTextAsync(privateFile,
            """{"Briefing":{"ApiKey":"synthetic-test-key","Provider":"Gemini"},"Database":{"Path":"private.db"}}""");
        var local = SyncConfiguration.Load(directory);
        using (local as IDisposable)
        {
            Check(SyncConfiguration.ReadApiKey(local) == "synthetic-test-key" && local["Briefing:Provider"] == "Gemini"
                && local["Briefing:MonthlyBudgetUsd"] == "10" && local["Database:Path"] == "private.db",
                "Private file overrides base settings without losing inherited options");
        }
        var external = Path.Combine(temporary, "external-settings.json");
        await File.WriteAllTextAsync(external, """{"Briefing":{"ApiKey":"external-test-key"}}""");
        var config = SyncConfiguration.Load(directory, external);
        using (config as IDisposable)
            Check(SyncConfiguration.ReadApiKey(config) == "external-test-key" && config["Briefing:Provider"] == "OpenAI",
                "Explicit private file replaces adjacent private settings and inherits base");
        Throws(() => SyncConfiguration.Load(directory, Path.Combine(temporary, "missing-private.json")), "Explicit private settings must exist");
        Throws(() => SyncConfiguration.Load(directory, "relative.json"), "Explicit settings must be absolute for scheduled tasks");
        await File.WriteAllTextAsync(privateFile, "{\"Briefing\": {\"ApiKey\": \"synthetic-secret-marker\", BROKEN }}");
        try
        {
            SyncConfiguration.Load(directory);
            throw new Exception("FAIL: Invalid private JSON must fail");
        }
        catch (InvalidOperationException exception)
        {
            Check(!exception.ToString().Contains("synthetic-secret-marker") && exception.InnerException is null,
                "Malformed private JSON cannot leak its contents through log exceptions");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private static void Throws(Action action, string message)
    {
        var threw = false;
        try { action(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { threw = true; }
        Check(threw, message);
    }
}
