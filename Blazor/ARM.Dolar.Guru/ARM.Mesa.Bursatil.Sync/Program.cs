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
        configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
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
    if (importIndex >= 0)
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
        exitCode = await new MarketSynchronizer(database, http, log).RunAsync(quotesOnly) ? 0 : 1;
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
