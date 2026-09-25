using System.Net;
using System.Text.Json;
using ARM.Dolar.Guru.Models;
using ARM.Dolar.Guru.Services;
using ARM.Dolar.Guru.Sync;
using Microsoft.Data.Sqlite;

// Executable integration checks, no network and no production database.
Environment.SetEnvironmentVariable("DOLAR_GURU_DB_PATH", null);
var temporary = Path.Combine(Path.GetTempPath(), "dolar-guru-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    var db = new GuruDatabase(Path.Combine(temporary, "market.db"));
    db.Initialize();
    db.Initialize();
    var service = new CotizacionesService(db);
    var empty = await service.ObtenerUltimasCotizacionesAsync();
    Check(empty.Item1.Count == 0 && empty.Item2.Count == 0, "Fresh database and repeated initialization");

    var quote = new ApiCotizacion { Nombre = "Blue", Casa = "blue", Moneda = "USD", Compra = 1234.56m, Venta = 1250.78m, FechaActualizacion = "2026-09-24T15:30:00Z" };
    var snapshot = JsonSerializer.Serialize(new[] { quote });
    db.SaveSnapshot("CotizacionesDolarJson", snapshot);
    db.SaveSnapshot("CotizacionesDolarJson", snapshot);
    Check(Count(db, "CotizacionesDolarJson") == 1, "Identical snapshots are not duplicated");
    var latest = (await service.ObtenerUltimasCotizacionesAsync()).Item1.Single();
    Check(latest.Compra == 1234.56m && latest.Venta == 1250.78m && latest.FechaActualizacion.Kind == DateTimeKind.Utc, "Decimal prices and UTC round trip");
    Check((await service.ObtenerHistoricoAgrupadoAsync(10))["Blue"].Count == 1, "Historical snapshot is readable");

    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => db.SaveSnapshot("CotizacionesDolarJson", snapshot))));
    Check(Count(db, "CotizacionesDolarJson") == 1, "Concurrent writers deduplicate with transactions");

    var file = Path.Combine(temporary, "legacy.json");
    var export = JsonSerializer.Serialize(new {
        CotizacionesDolarJson = new[] { new { FechaEjecucion = "2026-01-01T12:00:00", JsonData = snapshot } },
        News = new[] { new { Titulo = "Prueba", Url = "https://example.com/article", FechaPublicacion = "2026-01-01T12:00:00" } }
    });
    await File.WriteAllTextAsync(file, export);
    await LegacyImport.RunAsync(db, file);
    await LegacyImport.RunAsync(db, file);
    Check(Count(db, "CotizacionesDolarJson") == 2 && Count(db, "News") == 1, "Legacy import is repeatable without duplicates");
    Check((await new NewsService(db).ObtenerUltimasNoticiasAsync()).Single().Titulo == "Prueba", "Imported news can be queried");

    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new {
        CotizacionesDolarJson = new[] {
            new { FechaEjecucion = "2025-01-01T12:00:00", JsonData = snapshot },
            new { FechaEjecucion = "2025-01-02T12:00:00", JsonData = "invalid json" }
        }
    }));
    var failed = false;
    try { await LegacyImport.RunAsync(db, file); } catch (SqliteException) { failed = true; }
    Check(failed && Count(db, "CotizacionesDolarJson") == 2, "Invalid import rolls back every inserted row");

    using var http = new HttpClient(new StubHttp());
    var result = await new MarketSynchronizer(db, http).RunAsync(quotesOnly: true);
    Check(!result && Count(db, "CotizacionesDolarJson") == 2, "Failed dollar source preserves last good snapshot and returns failure");
    Check((await service.ObtenerUltimasCotizacionesAsync()).Item2.Single().Moneda == "EUR", "Another source still synchronizes after failure");
    Check((await service.ObtenerUltimaProyeccionAsync()).Count == 0, "Sparse history does not fabricate projections");
    Console.WriteLine("All 11 integration checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(temporary, recursive: true);
}

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

static long Count(GuruDatabase db, string table)
{
    using var connection = db.Open();
    using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM {table}";
    return (long)command.ExecuteScalar()!;
}

sealed class StubHttp : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/dolares")
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """[{"moneda":"EUR","nombre":"Euro","compra":1400.12,"venta":1450.34,"fechaActualizacion":"2026-09-24T15:30:00Z"}]""",
                System.Text.Encoding.UTF8, "application/json") });
}
