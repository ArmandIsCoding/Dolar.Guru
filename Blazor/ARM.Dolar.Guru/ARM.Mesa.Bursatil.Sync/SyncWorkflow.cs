using ARM.Mesa.Bursatil.Services;
using Microsoft.Extensions.Configuration;

namespace ARM.Mesa.Bursatil.Sync;

/// <summary>The normal scheduled/F5 workflow: refresh sources, then consider the editorial schedule.</summary>
public static class SyncWorkflow
{
    public static async Task<int> RunAsync(MarketDatabase database, IConfiguration configuration, ExecutionLog log)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MesaBursatil/2.0");
        var markets = new MarketSynchronizer(database, http, log, SyncConfiguration.ReadNewsOptions(configuration));
        return await RunAsync(markets, () => BriefingCommands.GenerateAsync(database, configuration, log), log);
    }

    public static async Task<int> RunAsync(MarketSynchronizer markets, Func<Task> generateBriefing, ExecutionLog log)
    {
        log.Info("Sincronización automática: cotizaciones y noticias; luego IA según appsettings y estado de la base. No requiere argumentos.");
        var exitCode = await markets.RunAsync() ? 0 : 1;
        try { await generateBriefing(); }
        catch (Exception ex)
        {
            // Failed AI must not roll back refreshed prices or leak provider payloads.
            log.Error($"Síntesis no generada ({ex.GetType().Name}). Revise configuración, cobertura y registro BriefingRuns; se conserva la edición anterior.");
            exitCode = 1;
        }
        return exitCode;
    }
}
