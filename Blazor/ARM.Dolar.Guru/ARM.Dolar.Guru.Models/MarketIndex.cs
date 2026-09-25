using System.Globalization;
using System.Text.Json.Serialization;

namespace ARM.Dolar.Guru.Models;

public sealed class MarketIndex
{
    [JsonPropertyName("especie")]
    public string Symbol { get; set; } = "";
    [JsonPropertyName("ultimo"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal Last { get; set; }
    [JsonPropertyName("variacion"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Change { get; set; }
    [JsonPropertyName("fecha")]
    public string Date { get; set; } = "";
    [JsonPropertyName("hora")]
    public string Time { get; set; } = "";
    [JsonPropertyName("sparkline30d")]
    public string? History { get; set; }

    [JsonIgnore]
    public decimal[] HistoryValues => (History ?? "").Split(',')
        .Select(value => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? (decimal?)number : null)
        .Where(value => value > 0).Select(value => value!.Value).ToArray();
}
