using System.Net;
using System.Text.Json;
using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;
using ARM.Mesa.Bursatil.Sync;
using Microsoft.Data.Sqlite;

// Executable integration checks, no network and no production database.
Environment.SetEnvironmentVariable("MESA_BURSATIL_DB_PATH", null);
var temporary = Path.Combine(Path.GetTempPath(), "mesa-bursatil-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    var db = new MarketDatabase(Path.Combine(temporary, "market.db"));
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
    Check((await service.ObtenerIndicesAsync()).Count == 0, "An existing database gains an empty indices table without data loss");
    var parsed = JsonSerializer.Deserialize<MarketIndex>("""{"especie":"NASDAQ 100","ultimo":"1234.56","variacion":null,"sparkline30d":"100.5,invalid,0,101.25"}""")!;
    Check(parsed.Last == 1234.56m && parsed.Change is null && parsed.HistoryValues.SequenceEqual(new[] { 100.5m, 101.25m }), "Rava numeric strings, missing variation and historical samples parse safely");
    var marketStub = new MarketStubHttp();
    using var marketHttp = new HttpClient(marketStub);
    var synchronizer = new MarketSynchronizer(db, marketHttp);
    Check(await synchronizer.RunAsync(), "Full synchronization works with independent mocked market and news sources");
    var indices = await service.ObtenerIndicesAsync();
    Check(indices.Count == 8 && indices[0].Symbol == "NASDAQ 100" && indices[0].Last == 1234.56m
        && indices[0].HistoryValues.Length == 3 && indices[0].Time == "18:59", "Global instruments, source timestamp and history survive SQLite round trip");
    Check(await synchronizer.RunAsync() && Count(db, "IndicesMercadoJson") == 1, "Repeated market captures deduplicate");
    marketStub.IncompleteIndices = true;
    Check(!await synchronizer.RunAsync() && Count(db, "IndicesMercadoJson") == 1
        && (await service.ObtenerIndicesAsync()).Count == 8, "Partial indices response preserves the last complete capture");
    Check((await service.ObtenerSeriesVentaAsync())["Blue"].All(point => point.Venta == 1250.78m), "Card history uses selling prices, not buying prices");
    Console.WriteLine("All 18 integration checks passed.");
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

static long Count(MarketDatabase db, string table)
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

sealed class MarketStubHttp : HttpMessageHandler
{
    public bool IncompleteIndices { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string[] symbols = ["NASDAQ 100", "S&P 500", "DOW JONES", "MERVAL", "RIESGO PAIS", "ORO (F)", "PETROLEO WTI (F)", "SOJA CHICAGO"];
        var path = request.RequestUri!.AbsolutePath;
        var content = path switch
        {
            "/v1/dolares" => """[{"moneda":"USD","nombre":"Blue","casa":"blue","compra":1234.56,"venta":1250.78,"fechaActualizacion":"2026-09-24T15:30:00Z"}]""",
            "/v1/cotizaciones" => """[{"moneda":"EUR","nombre":"Euro","compra":1400.12,"venta":1450.34,"fechaActualizacion":"2026-09-24T15:30:00Z"}]""",
            "/api/prices/indices" => JsonSerializer.Serialize(new { datos = symbols.Take(IncompleteIndices ? 7 : 8).Select(symbol => new {
                especie = symbol, ultimo = "1234.56", variacion = -0.25m, fecha = "2026-09-24T00:00:00Z", hora = "18:59", sparkline30d = "1200,1220,1234.56"
            }) }),
            "/api/prices/rofex" => """{"datos":[{"especie":"DLR/SEP26","ultimo":"1524.5","vencimiento":"2026-09-30"}]}""",
            _ => """<rss version="2.0"><channel><title>Test feed</title><link>https://example.com</link><description>Integration fixture</description></channel></rss>"""
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, System.Text.Encoding.UTF8, path.StartsWith("/v1/") || path.StartsWith("/api/") ? "application/json" : "application/rss+xml") });
    }
}
