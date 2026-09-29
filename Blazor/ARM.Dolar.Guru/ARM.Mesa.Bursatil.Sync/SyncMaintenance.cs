using ARM.Mesa.Bursatil.Services;
using Microsoft.Extensions.Configuration;

namespace ARM.Mesa.Bursatil.Sync;

/// <summary>Optional legacy/admin commands. The scheduled job and F5 require no arguments.</summary>
public static class SyncMaintenance
{
    public static string? ReadPath(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--") || !Path.IsPathFullyQualified(args[index + 1]))
            throw new ArgumentException($"{option} requiere una ruta absoluta.");
        return args[index + 1];
    }

    // Null means run the regular full sync, including when only a settings/database override was supplied.
    public static async Task<int?> TryRunAsync(string[] args, MarketDatabase database, IConfiguration configuration, ExecutionLog log)
    {
        if (args.Length == 0) return null;
        if (args.Contains("--briefing-check"))
            BriefingCommands.CheckConfiguration(configuration, log.Info);
        else if (BriefingCommands.HandleReview(args, database)) { }
        else if (args.Contains("--briefing-generate"))
        {
            using var http = new HttpClient();
            new NewsFeedSynchronizer(database, http).ApplySourcePolicy(SyncConfiguration.ReadNewsOptions(configuration).Sources);
            await BriefingCommands.GenerateAsync(database, configuration, log);
        }
        else if (Array.IndexOf(args, "--import") is var index && index >= 0)
        {
            if (index + 1 >= args.Length) throw new ArgumentException("--import requiere un archivo JSON.");
            await LegacyImport.RunAsync(database, args[index + 1], log);
        }
        else if (args.Contains("--initialize-only")) log.Info("Modo: solo inicialización.");
        else if (args.Contains("--quotes-only"))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MesaBursatil/2.0");
            return await new MarketSynchronizer(database, http, log).RunAsync(quotesOnly: true) ? 0 : 1;
        }
        else return null;
        return 0;
    }
}
