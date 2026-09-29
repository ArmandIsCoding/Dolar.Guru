using System.Diagnostics;
using ARM.Mesa.Bursatil.Services;
using ARM.Mesa.Bursatil.Sync;
using Microsoft.Extensions.Configuration;

var elapsed = Stopwatch.StartNew();
ExecutionLog? log = null;
var exitCode = 1;
try
{
    IConfigurationRoot configuration;
    try
    {
        // Scheduled tasks may start in System32: always load settings beside the executable.
        var settingsIndex = Array.IndexOf(args, "--settings");
        if (settingsIndex >= 0 && (settingsIndex + 1 >= args.Length || args[settingsIndex + 1].StartsWith("--")))
            throw new ArgumentException("--settings requiere una ruta absoluta al archivo privado.");
        configuration = SyncConfiguration.Load(AppContext.BaseDirectory,
            settingsIndex >= 0 ? args[settingsIndex + 1] : null);
    }
    catch
    {
        // Settings cannot be read: retain the startup exception in the default directory.
        log = new ExecutionLog(ExecutionLog.DefaultDirectory);
        throw;
    }

    log = new ExecutionLog(configuration["Logging:Directory"] ?? ExecutionLog.DefaultDirectory);
    log.Info($"Inicio de ejecución. PID={Environment.ProcessId}; Equipo={Environment.MachineName}; Directorio de trabajo={Environment.CurrentDirectory}");
    log.Info($"Archivo de log: {log.FilePath}");

    var pathIndex = Array.IndexOf(args, "--database");
    if (pathIndex >= 0 && pathIndex + 1 >= args.Length)
        throw new ArgumentException("--database requires an absolute file path.");
    var database = new MarketDatabase(pathIndex >= 0 ? args[pathIndex + 1] : configuration["Database:Path"]);
    log.Info($"SQLite: {database.FilePath}");
    database.Initialize();

    var importIndex = Array.IndexOf(args, "--import");
    if (BriefingCommands.HandleReview(args, database))
    {
        exitCode = 0;
    }
    else if (args.Contains("--briefing-generate"))
    {
        using var newsHttp = new HttpClient();
        new NewsFeedSynchronizer(database, newsHttp).ApplySourcePolicy(ReadNewsOptions(configuration).Sources);
        await BriefingCommands.GenerateAsync(database, configuration, log);
        exitCode = 0;
    }
    else if (importIndex >= 0)
    {
        log.Info("Modo: importación.");
        if (importIndex + 1 >= args.Length) throw new ArgumentException("--import requires a JSON file.");
        await LegacyImport.RunAsync(database, args[importIndex + 1], log);
        exitCode = 0;
    }
    else if (args.Contains("--initialize-only"))
    {
        log.Info("Modo: solo inicialización.");
        exitCode = 0;
    }
    else
    {
        var quotesOnly = args.Contains("--quotes-only");
        log.Info(quotesOnly ? "Modo: solo cotizaciones." : "Modo: sincronización completa.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MesaBursatil/2.0");
        exitCode = await new MarketSynchronizer(database, http, log, ReadNewsOptions(configuration)).RunAsync(quotesOnly) ? 0 : 1;
        if (!quotesOnly)
        {
            try { await BriefingCommands.GenerateAsync(database, configuration, log); }
            catch (Exception ex)
            {
                // News/AI errors never roll back market snapshots. Avoid logging provider payloads.
                log.Error($"Síntesis no generada ({ex.GetType().Name}). Revise configuración, cobertura y registro BriefingRuns; se conserva la edición anterior.");
                exitCode = 1;
            }
        }
    }
}
catch (Exception ex)
{
    if (log is not null) log.Error("La ejecución falló.", ex);
    else Console.Error.WriteLine($"No se pudo iniciar el log. Revise Logging:Directory y los permisos de escritura.{Environment.NewLine}{ex}");
}
finally
{
    Environment.ExitCode = exitCode;
    if (log is not null)
    {
        try
        {
            var summary = $"Fin de ejecución. Resultado={(exitCode == 0 ? "CORRECTO" : "ERROR")}; Código de salida={exitCode}; Duración={elapsed.Elapsed.TotalSeconds:F3} s.";
            if (exitCode == 0) log.Info(summary); else log.Error(summary);
        }
        finally { log.Dispose(); }
    }
}

static NewsFeedOptions ReadNewsOptions(IConfiguration configuration)
{
    var section = configuration.GetSection("News:Sources");
    if (!section.GetChildren().Any()) return new NewsFeedOptions();
    return new NewsFeedOptions
    {
        Sources = section.GetChildren().Select(s => new NewsFeedSource
        {
            Id = s["Id"] ?? "", Name = s["Name"] ?? "", Url = s["Url"] ?? "",
            PublisherGroup = s["PublisherGroup"] ?? "",
            IsInternational = bool.TryParse(s["IsInternational"], out var international) && international,
            Enabled = !bool.TryParse(s["Enabled"], out var enabled) || enabled,
            AllowAiUse = bool.TryParse(s["AllowAiUse"], out var allowed) && allowed
        }).ToList()
    };
}
