using System.Text;

namespace ARM.Mesa.Bursatil.Sync;

/// <summary>A separate, immediately flushed UTF-8 log for each process invocation.</summary>
public sealed class ExecutionLog : IDisposable
{
    public const string DefaultDirectory = @"C:\logs sitios IIS";
    private readonly StreamWriter writer;
    private readonly object gate = new();
    public string FilePath { get; }

    public ExecutionLog(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new ArgumentException("Logging:Directory debe ser una ruta absoluta válida.", nameof(directory));

        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory,
            $"ARM.Mesa.Bursatil.Sync_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fffffff}Z_{Environment.ProcessId}_{Guid.NewGuid():N}.log");
        writer = new StreamWriter(new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        lock (gate)
        {
            var entry = $"{DateTimeOffset.Now:O} [{level}] {message}";
            writer.WriteLine(entry);
            (level == "ERROR" ? Console.Error : Console.Out).WriteLine(entry);
        }
    }

    public void Dispose() => writer.Dispose();
}
