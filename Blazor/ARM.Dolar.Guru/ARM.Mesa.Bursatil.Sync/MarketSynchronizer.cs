using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;
using HtmlAgilityPack;

namespace ARM.Mesa.Bursatil.Sync;

public sealed class MarketSynchronizer(MarketDatabase database, HttpClient http)
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
            foreach (var url in new[] { "https://www.clarin.com/rss/economia/", "https://www.perfil.com/feed" })
                results.Add(await Attempt(url, () => News(url)));
        }
        results.Add(await Attempt("Escenarios estadísticos", Projections));
        return results.All(success => success);
    }

    private static async Task<bool> Attempt(string source, Func<Task> action)
    {
        try { await action(); Console.WriteLine($"OK · {source}"); return true; }
        catch (Exception ex) { Console.Error.WriteLine($"ERROR · {source}: {ex.Message}"); return false; }
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

    private async Task News(string url)
    {
        using var stream = await http.GetStreamAsync(url);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000
        });
        var feed = SyndicationFeed.Load(reader);
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var item in feed.Items.Take(50))
        {
            var link = item.Links.FirstOrDefault()?.Uri;
            if (string.IsNullOrWhiteSpace(item.Title?.Text) || link is null ||
                !link.IsAbsoluteUri || (link.Scheme != "https" && link.Scheme != "http")) continue;
            var document = new HtmlDocument();
            document.LoadHtml(item.Summary?.Text ?? "");
            var summary = WebUtility.HtmlDecode(document.DocumentNode.InnerText);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO News(Titulo, Resumen, Url, Fuente, FechaPublicacion)
                VALUES($title,$summary,$url,$source,$date) ON CONFLICT(Url) DO NOTHING
                """;
            command.Parameters.AddWithValue("$title", item.Title!.Text.Trim());
            command.Parameters.AddWithValue("$summary", summary);
            command.Parameters.AddWithValue("$url", link.ToString());
            command.Parameters.AddWithValue("$source", feed.Title?.Text ?? new Uri(url).Host);
            command.Parameters.AddWithValue("$date", item.PublishDate.UtcDateTime);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
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
