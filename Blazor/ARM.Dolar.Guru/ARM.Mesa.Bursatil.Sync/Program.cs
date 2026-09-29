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
        configuration = SyncConfiguration.Load(AppContext.BaseDirectory, SyncMaintenance.ReadPath(args, "--settings"));
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

    var database = new MarketDatabase(SyncMaintenance.ReadPath(args, "--database") ?? configuration["Database:Path"]);
    log.Info($"SQLite: {database.FilePath}");
    database.Initialize();

    // F5 and the Windows task use this normal path with no arguments. CLI options
    // remain isolated for maintenance/review and backwards-compatible deployments.
    exitCode = await SyncMaintenance.TryRunAsync(args, database, configuration, log)
        ?? await SyncWorkflow.RunAsync(database, configuration, log);
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
