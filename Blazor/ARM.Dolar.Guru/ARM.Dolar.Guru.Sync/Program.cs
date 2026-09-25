using ARM.Dolar.Guru.Services;
using ARM.Dolar.Guru.Sync;

// Both IIS and the scheduled task must use the same absolute database path.
var pathIndex = Array.IndexOf(args, "--database");
if (pathIndex >= 0 && pathIndex + 1 >= args.Length)
    throw new ArgumentException("--database requires an absolute file path.");
var database = new GuruDatabase(pathIndex >= 0 ? args[pathIndex + 1] : null);
database.Initialize();
Console.WriteLine($"SQLite: {database.FilePath}");

var importIndex = Array.IndexOf(args, "--import");
if (importIndex >= 0)
{
    if (importIndex + 1 >= args.Length) throw new ArgumentException("--import requires a JSON file.");
    await LegacyImport.RunAsync(database, args[importIndex + 1]);
    return;
}
if (args.Contains("--initialize-only")) return;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("DolarGuru/2.0");
var synchronizer = new MarketSynchronizer(database, http);
Environment.ExitCode = await synchronizer.RunAsync(args.Contains("--quotes-only")) ? 0 : 1;
