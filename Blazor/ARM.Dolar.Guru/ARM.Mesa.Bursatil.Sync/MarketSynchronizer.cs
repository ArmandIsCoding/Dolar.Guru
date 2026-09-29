using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;

namespace ARM.Mesa.Bursatil.Sync;

public sealed class MarketSynchronizer(MarketDatabase database, HttpClient http, ExecutionLog? log = null, NewsFeedOptions? newsOptions = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> RunAsync(bool quotesOnly = false)
    {
        // Each source fails independently: a broken RSS feed must never block prices.
        var results = new List<bool>
        {
            await Attempt("Dólar", () => Quotes<ApiCotizacion>("https://dolarapi.com/v1/dolares", "CotizacionesDolarJson")),
            await Attempt("Divisas", () => Quotes<ApiCotizacionOtros>("https://dolarapi.com/v1/cotizaciones", "CotizacionesOtrosJson"))
        };
        if (!quotesOnly)
        {
            results.Add(await Attempt("Índices y commodities Rava", Indices));
            results.Add(await Attempt("Futuros Rava", Futures));
            var news = new NewsFeedSynchronizer(database, http);
            var options = newsOptions ?? new NewsFeedOptions();
            results.Add(await Attempt("Registro de fuentes RSS", () =>
            {
                NewsSchema.Initialize(database);
                news.ApplySourcePolicy(options.Sources);
                return Task.CompletedTask;
            }));
            foreach (var source in options.Sources.Where(source => source.Enabled))
                results.Add(await Attempt($"RSS · {source.Name}", async () =>
                    await news.RunAsync(source, options.MaxItemsPerFeed)));
        }
        results.Add(await Attempt("Escenarios estadísticos", Projections));
        return results.All(success => success);
    }

    private async Task<bool> Attempt(string source, Func<Task> action)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        log?.Info($"Inicio de fuente: {source}");
        try
        {
            await action();
            var message = $"OK · {source}; Duración={elapsed.Elapsed.TotalSeconds:F3} s";
            if (log is not null) log.Info(message); else Console.WriteLine(message);
            return true;
        }
        catch (Exception ex)
        {
            var message = $"ERROR · {source}; Duración={elapsed.Elapsed.TotalSeconds:F3} s";
            if (log is not null) log.Error(message, ex); else Console.Error.WriteLine($"{message}: {ex}");
            return false;
        }
    }

    private async Task Quotes<T>(string url, string table)
    {
        var data = await http.GetFromJsonAsync<List<T>>(url);
        if (data is not { Count: > 0 }) throw new InvalidDataException("La fuente devolvió una lista vacía.");
        // Do not replace the last good snapshot with a partial or malformed response.
        var valid = data.All(item => item switch
        {
            ApiCotizacion c => ValidQuote(c.Nombre, c.Compra, c.Venta, c.FechaActualizacion),
            ApiCotizacionOtros c => ValidQuote(c.Nombre, c.Compra, c.Venta, c.FechaActualizacion),
            _ => false
        });
        if (!valid) throw new InvalidDataException("Cotización inválida; se conservan los datos anteriores.");
        database.SaveSnapshot(table, JsonSerializer.Serialize(data));
    }

    private static bool ValidQuote(string name, decimal buy, decimal sell, string date) =>
        !string.IsNullOrWhiteSpace(name) && buy >= 0 && sell > 0 &&
        DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private async Task Futures()
    {
        // Public endpoint used by Rava's current futures page (the old futuros-p element was removed).
        using var json = JsonDocument.Parse(await http.GetStringAsync("https://mercado.rava.com/api/prices/rofex"));
        var contracts = json.RootElement.GetProperty("datos").Deserialize<List<FuturoRavaRofex>>(JsonOptions)?
            .Where(c => c.Especie.StartsWith("DLR/", StringComparison.OrdinalIgnoreCase)
                && !c.Especie.EndsWith("M") && c.Especie != "DLR/SPOT")
            .OrderBy(c => c.Vencimiento).ToList();
        if (contracts is not { Count: > 0 }) throw new InvalidDataException("Sin contratos.");
        database.SaveSnapshot("FuturoRavaJson", JsonSerializer.Serialize(contracts));
    }

    private async Task Indices()
    {
        using var json = JsonDocument.Parse(await http.GetStringAsync("https://mercado.rava.com/api/prices/indices"));
        string[] symbols = ["NASDAQ 100", "S&P 500", "DOW JONES", "MERVAL", "RIESGO PAIS", "ORO (F)", "PETROLEO WTI (F)", "SOJA CHICAGO"];
        var indices = json.RootElement.GetProperty("datos").Deserialize<List<MarketIndex>>(JsonOptions)?
            .Where(item => symbols.Contains(item.Symbol)).OrderBy(item => Array.IndexOf(symbols, item.Symbol)).ToList();
        if (indices is null || symbols.Any(symbol => indices.Count(item => item.Symbol == symbol) != 1)
            || indices.Any(item => item.Last <= 0 || !DateTime.TryParse(item.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            throw new InvalidDataException("Índices incompletos o inválidos; se conserva la captura anterior.");
        database.SaveSnapshot("IndicesMercadoJson", JsonSerializer.Serialize(indices));
    }

    private async Task Projections()
    {
        var service = new CotizacionesService(database);
        var history = await service.ObtenerHistoricoAgrupadoAsync(10000);
        var projections = new List<ProyeccionDolar>();
        foreach (var (name, samples) in history)
        {
            // One observation per day, not seven polling intervals mislabeled as a week.
            var days = samples.GroupBy(x => x.Fecha.Date).Select(g => g.Last()).OrderBy(x => x.Fecha).ToList();
            if (days.Count < 5) continue;
            var last = days[^1];
            var week = days.Where(x => x.Fecha >= last.Fecha.AddDays(-7)).Average(x => x.Compra);
            var month = days.Where(x => x.Fecha >= last.Fecha.AddDays(-30)).Average(x => x.Compra);
            projections.Add(new ProyeccionDolar
            {
                Nombre = name, Icono = "", ValorSemana = Math.Max(0, Math.Round(2 * last.Compra - week, 2)),
                ValorMes = Math.Max(0, Math.Round(2 * last.Compra - month, 2)),
                Confianza = Math.Min(1, days.Count / 30.0),
                Justificacion = $"Extrapolación respecto del promedio de 7 y 30 días. {days.Count} días observados; no es una probabilidad ni un modelo de IA."
            });
        }
        database.SaveSnapshot("ProyeccionesDolarJson", JsonSerializer.Serialize(projections));
    }
}
