namespace ARM.Dolar.Guru.Services
{
    using ARM.Dolar.Guru.Models;
    using Microsoft.Data.SqlClient;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Text.Json;

    public class CotizacionesService
    {
        private readonly string connectionString = "Server=server;Database=DolarGuru;User Id=sa;Password=123456;TrustServerCertificate=True;";

        public async Task<List<ProyeccionDolar>> ObtenerUltimaProyeccionAsync()
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT TOP 1 JsonData FROM ProyeccionesDolarJson ORDER BY FechaEjecucion DESC", conn);
            var json = (string?)await cmd.ExecuteScalarAsync();

            return json is null
                ? new List<ProyeccionDolar>()
                : JsonSerializer.Deserialize<List<ProyeccionDolar>>(json) ?? new();
        }

        public async Task<Dictionary<string, List<(DateTime Fecha, decimal Compra)>>> ObtenerHistoricoAgrupadoAsync(int take)
        {
            var resultado = new Dictionary<string, List<(DateTime, decimal)>>();

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand($"SELECT TOP {take} FechaEjecucion, JsonData FROM CotizacionesDolarJson ORDER BY FechaEjecucion DESC", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fecha = reader.GetDateTime(0);
                var json = reader.GetString(1);
                var cotizaciones = JsonSerializer.Deserialize<List<ApiCotizacion>>(json);

                foreach (var c in cotizaciones ?? Enumerable.Empty<ApiCotizacion>())
                {
                    if (!resultado.TryGetValue(c.Nombre, out List<(DateTime, decimal)>? value))
                    {
                        value = [];
                        resultado[c.Nombre] = value;
                    }

                    value.Add((fecha, c.Compra));
                }
            }

            // Ordenar cada lista cronológicamente
            foreach (var key in resultado.Keys.ToList())
            {
                resultado[key] = [.. resultado[key].OrderBy(x => x.Item1)];
            }

            return resultado;
        }

        public async Task<List<(DateTime FechaVencimiento, decimal Ultimo)>> ObtenerHistoricoFuturosAsync()
        {
            var lista = new List<(DateTime FechaVencimiento, decimal Ultimo)>();

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT TOP 1 FechaEjecucion, JsonData FROM FuturoRavaJson ORDER BY FechaEjecucion DESC", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fechaEjecucion = reader.GetDateTime(0);
                var json = reader.GetString(1);

                var contratos = JsonSerializer.Deserialize<List<FuturoRavaRofex>>(json);

                foreach (var c in contratos ?? Enumerable.Empty<FuturoRavaRofex>())
                {
                    // Primero, nos aseguramos de que el vencimiento sea una fecha válida
                    if (DateTime.TryParse(c.Vencimiento, out var fechaVto))
                    {
                        // Declaramos la variable que va a contener el resultado de la conversión
                        decimal ultimoValor;

                        // Ahora, intentamos convertir el string 'Ultimo' a decimal de forma SEGURA
                        if (decimal.TryParse(c.Ultimo, NumberStyles.Any, CultureInfo.InvariantCulture, out ultimoValor))
                        {
                            // Solo si AMBAS conversiones (fecha y decimal) son exitosas,
                            // agregamos el par de valores a la lista.
                            lista.Add((fechaVto, ultimoValor));
                        }
                        // Si decimal.TryParse falla (porque c.Ultimo está vacío o no es un número),
                        // simplemente no hacemos nada y continuamos con el siguiente contrato. ¡Sin errores!
                    }
                }
            }

            // Ordenar por vencimiento
            return lista.OrderBy(x => x.FechaVencimiento).ToList();
        }

        public async Task<(List<Cotizacion>, List<CotizacionOtros>)> ObtenerUltimasCotizacionesAsync()
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmdDolar = new SqlCommand("SELECT TOP 1 JsonData FROM CotizacionesDolarJson ORDER BY FechaEjecucion DESC", conn);
            var jsonDolar = (string?)await cmdDolar.ExecuteScalarAsync() ?? "[]";

            var cmdOtros = new SqlCommand("SELECT TOP 1 JsonData FROM CotizacionesOtrosJson ORDER BY FechaEjecucion DESC", conn);
            var jsonOtros = (string?)await cmdOtros.ExecuteScalarAsync() ?? "[]";

            var listaDolar = JsonSerializer.Deserialize<List<ApiCotizacion>>(jsonDolar)?
                .Select(c => new Cotizacion(
                    Nombre: c.Nombre,
                    Compra: c.Compra,
                    Venta: c.Venta,
                    FechaActualizacion: DateTime.Parse(c.FechaActualizacion, null, System.Globalization.DateTimeStyles.AdjustToUniversal))
                ).ToList() ?? new();

            var listaOtros = JsonSerializer.Deserialize<List<ApiCotizacionOtros>>(jsonOtros)?
                .Select(c => new CotizacionOtros(
                    Moneda: c.Moneda,
                    Nombre: c.Nombre,
                    Compra: c.Compra,
                    Venta: c.Venta,
                    FechaActualizacion: DateTime.Parse(c.FechaActualizacion, null, System.Globalization.DateTimeStyles.AdjustToUniversal))
                ).ToList() ?? new();

            return (listaDolar, listaOtros);
        }

        public async Task<List<(DateTime Fecha, decimal Compra, decimal Venta)>> ObtenerHistoricoAsync(string nombreCotizacion)
        {
            var historico = new List<(DateTime, decimal, decimal)>();
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT TOP 30 FechaEjecucion, JsonData FROM CotizacionesDolarJson ORDER BY FechaEjecucion DESC", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fecha = reader.GetDateTime(0);
                var json = reader.GetString(1);
                var cotizaciones = JsonSerializer.Deserialize<List<ApiCotizacion>>(json);

                var cotizacion = cotizaciones?.FirstOrDefault(c => c.Nombre == nombreCotizacion);
                if (cotizacion != null)
                {
                    var compra = cotizacion.Compra;
                    var venta = cotizacion.Venta;
                    historico.Add((fecha, compra, venta));
                }
            }

            return [.. historico.OrderBy(h => h.Item1)];
        }

        public async Task<List<FuturoRavaRofex>> ObtenerUltimosFuturosRavaAsync()
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT TOP 1 JsonData FROM FuturoRavaJson ORDER BY FechaEjecucion DESC", conn);
            var json = (string?)await cmd.ExecuteScalarAsync();

            return json is null ? [] : JsonSerializer.Deserialize<List<FuturoRavaRofex>>(json) ?? [];
        }

    }
}
