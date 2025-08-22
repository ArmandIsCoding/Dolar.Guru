using ARM.Dolar.Guru.Models;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ARM.Dolar.Guru.Sync
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("⏳ Descargando cotizaciones...");

            var http = new HttpClient();

            // Descargar datos
            var resultDolar = await http.GetFromJsonAsync<List<ApiCotizacion>>("https://dolarapi.com/v1/dolares");
            var resultOtros = await http.GetFromJsonAsync<List<ApiCotizacionOtros>>("https://dolarapi.com/v1/cotizaciones");

            if (resultDolar == null || resultOtros == null)
            {
                Console.WriteLine("❌ Error al descargar los datos. Verifica la conexión a Internet o la API.");
                return;
            }

            var cotizacionesDolar = resultDolar?.Select(c => (dynamic)new
            {
                c.Nombre,
                c.Compra,
                c.Venta,
                Fecha = DateTime.Parse(c.FechaActualizacion, null, DateTimeStyles.AdjustToUniversal),
                c.Casa,
                c.Moneda
            }).ToList();

            var cotizacionesOtros = resultOtros?.Select(c => (dynamic)new
            {
                c.Nombre,
                c.Moneda,
                c.Compra,
                c.Venta,
                Fecha = DateTime.Parse(c.FechaActualizacion, null, DateTimeStyles.AdjustToUniversal),
                c.Casa
            }).ToList();

            Console.WriteLine("✅ Datos descargados. Guardando en la base...");

            await GuardarEnBaseJson(resultDolar, resultOtros);

            Console.WriteLine("🎉 Proceso finalizado.");
        }

        static async Task GuardarEnBaseJson(List<ApiCotizacion>? cotizacionesDolar, List<ApiCotizacionOtros>? cotizacionesOtros)
        {
            var connectionString = "Server=server;Database=DolarGuru;User Id=sa;Password=123456;TrustServerCertificate=True;";

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var jsonDolar = JsonSerializer.Serialize(cotizacionesDolar ?? new());
            var jsonOtros = JsonSerializer.Serialize(cotizacionesOtros ?? new());

            var cmdDolar = new SqlCommand("INSERT INTO CotizacionesDolarJson (JsonData) VALUES (@Json)", conn);
            cmdDolar.Parameters.AddWithValue("@Json", jsonDolar);
            await cmdDolar.ExecuteNonQueryAsync();

            var cmdOtros = new SqlCommand("INSERT INTO CotizacionesOtrosJson (JsonData) VALUES (@Json)", conn);
            cmdOtros.Parameters.AddWithValue("@Json", jsonOtros);
            await cmdOtros.ExecuteNonQueryAsync();
        }

    }
}