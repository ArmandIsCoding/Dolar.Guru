using System.Globalization;
using System.Text.Json;
using ARM.Dolar.Guru.Models;

namespace ARM.Dolar.Guru.Services;

public sealed class CotizacionesService(GuruDatabase database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private List<(DateTime Fecha, List<T> Items)> Read<T>(string table, int count)
    {
        if (!GuruDatabase.SnapshotTables.Contains(table)) throw new ArgumentException("Unknown snapshot type", nameof(table));
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT FechaEjecucion, JsonData FROM {table} ORDER BY FechaEjecucion DESC, Id DESC LIMIT $count";
        command.Parameters.AddWithValue("$count", Math.Clamp(count, 1, 100000));
        using var reader = command.ExecuteReader();
        var rows = new List<(DateTime, List<T>)>();
        while (reader.Read())
            rows.Add((DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                JsonSerializer.Deserialize<List<T>>(reader.GetString(1), JsonOptions) ?? []));
        return rows;
    }

    public Task<List<ProyeccionDolar>> ObtenerUltimaProyeccionAsync() =>
        Task.FromResult(Read<ProyeccionDolar>("ProyeccionesDolarJson", 1).FirstOrDefault().Items ?? []);

    public Task<List<FuturoRavaRofex>> ObtenerUltimosFuturosRavaAsync() =>
        Task.FromResult(Read<FuturoRavaRofex>("FuturoRavaJson", 1).FirstOrDefault().Items ?? []);

    public Task<List<MarketIndex>> ObtenerIndicesAsync() =>
        Task.FromResult(Read<MarketIndex>("IndicesMercadoJson", 1).FirstOrDefault().Items ?? []);

    public Task<Dictionary<string, List<(DateTime Fecha, decimal Venta)>>> ObtenerSeriesVentaAsync() =>
        Task.FromResult(Read<ApiCotizacion>("CotizacionesDolarJson", 50)
            .SelectMany(row => row.Items.Select(c => (c.Nombre, row.Fecha, c.Venta)))
            .GroupBy(c => c.Nombre).ToDictionary(g => g.Key,
                g => g.OrderBy(x => x.Fecha).Select(x => (x.Fecha, x.Venta)).ToList()));

    public Task<(List<Cotizacion>, List<CotizacionOtros>)> ObtenerUltimasCotizacionesAsync()
    {
        var dollars = (Read<ApiCotizacion>("CotizacionesDolarJson", 1).FirstOrDefault().Items ?? [])
            .Select(c => new Cotizacion(c.Nombre, c.Compra, c.Venta, ParseDate(c.FechaActualizacion))).ToList();
        var others = (Read<ApiCotizacionOtros>("CotizacionesOtrosJson", 1).FirstOrDefault().Items ?? [])
            .Select(c => new CotizacionOtros(c.Moneda, c.Nombre, c.Compra, c.Venta, ParseDate(c.FechaActualizacion))).ToList();
        return Task.FromResult((dollars, others));
    }

    public Task<Dictionary<string, List<(DateTime Fecha, decimal Compra)>>> ObtenerHistoricoAgrupadoAsync(int take)
    {
        var history = Read<ApiCotizacion>("CotizacionesDolarJson", take)
            .SelectMany(row => row.Items.Select(c => (c.Nombre, row.Fecha, c.Compra)))
            .GroupBy(c => c.Nombre)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Fecha).Select(x => (x.Fecha, x.Compra)).ToList());
        return Task.FromResult(history);
    }

    public Task<List<(DateTime Fecha, decimal Compra, decimal Venta)>> ObtenerHistoricoAsync(string nombreCotizacion) =>
        Task.FromResult(Read<ApiCotizacion>("CotizacionesDolarJson", 100000)
            .SelectMany(row => row.Items.Where(c => c.Nombre == nombreCotizacion)
                .Select(c => (row.Fecha, c.Compra, c.Venta))).OrderBy(x => x.Fecha).ToList());

    public async Task<List<(DateTime FechaVencimiento, decimal Ultimo)>> ObtenerHistoricoFuturosAsync()
    {
        var result = new List<(DateTime FechaVencimiento, decimal Ultimo)>();
        foreach (var contract in await ObtenerUltimosFuturosRavaAsync())
            if (DateTime.TryParse(contract.Vencimiento, CultureInfo.GetCultureInfo("es-AR"), DateTimeStyles.None, out var date)
                && decimal.TryParse(contract.Ultimo, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                result.Add((date, value));
        return result.OrderBy(x => x.FechaVencimiento).ToList();
    }

    private static DateTime ParseDate(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date) ? date : DateTime.MinValue;
}
