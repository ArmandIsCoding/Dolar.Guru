using ARM.Dolar.Guru.Models;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;


namespace ARM.Dolar.Guru.Sync
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine("🚀 Iniciando proceso de sincronización...");

            // Conexión a la base de datos
            var connectionString = "Server=server;Database=DolarGuru;User Id=sa;Password=123456;TrustServerCertificate=True;";
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            Console.WriteLine("🧾 Extrayendo Dólar Futuro Rava...");
            var futuros = await ExtraerDolarFuturoRavaAsync(conn);
            Console.WriteLine($"✅ {futuros.Count} contratos extraídos y guardados en la base.");

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

            await GuardarEnBaseJson(resultDolar, resultOtros, conn);

            Console.WriteLine("🧠 Generando proyecciones...");
            var proyecciones = await GenerarProyeccionesAsync(conn);
            await GuardarProyeccionesSiCambianAsync(conn, proyecciones);


            await conn.CloseAsync();

            reloj.Stop();
            RegistrarLog($"✅ Proceso finalizado en {reloj.Elapsed.TotalSeconds:F1} segundos.");

            Console.WriteLine("🎉 Proceso finalizado.");
        }


        public static async Task<List<FuturoRavaRofex>> ExtraerDolarFuturoRavaAsync(SqlConnection conn)
        {
            var url = "https://www.rava.com/cotizaciones/futuros";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodo = doc.DocumentNode.SelectSingleNode("//futuros-p");
            var datosRaw = nodo?.GetAttributeValue(":datos", null);

            if (string.IsNullOrWhiteSpace(datosRaw))
                return new();

            var jsonClean = HttpUtility.HtmlDecode(datosRaw);
            var jsonDoc = JsonDocument.Parse(jsonClean);

            // 👉 Extraemos solo el bloque ROFEX
            var futurosJson = jsonDoc.RootElement.GetProperty("ROFEX").GetRawText();

            // ✅ Guardamos el JSON en la base
            await GuardarFuturosJsonAsync(conn, futurosJson);

            // ✅ Deserializamos los contratos
            var contratos = JsonSerializer.Deserialize<List<FuturoRavaRofex>>(futurosJson);

            return contratos ?? [];
        }

        static async Task GuardarFuturosJsonAsync(SqlConnection conn, string futurosJson)
        {
            var cmd = new SqlCommand("INSERT INTO FuturoRavaJson (JsonData) VALUES (@Json)", conn);
            cmd.Parameters.AddWithValue("@Json", futurosJson);
            await cmd.ExecuteNonQueryAsync();
        }


        static async Task GuardarEnBaseJson(List<ApiCotizacion>? cotizacionesDolar, List<ApiCotizacionOtros>? cotizacionesOtros, SqlConnection conn)
        {


            var jsonDolar = JsonSerializer.Serialize(cotizacionesDolar ?? new());
            var jsonOtros = JsonSerializer.Serialize(cotizacionesOtros ?? new());

            var ultimoJsonDolar = await ObtenerUltimoJson(conn, "CotizacionesDolarJson");
            var ultimoJsonOtros = await ObtenerUltimoJson(conn, "CotizacionesOtrosJson");

            var ahora = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            if (jsonDolar == ultimoJsonDolar && jsonOtros == ultimoJsonOtros)
            {
                var mensaje = $"🟡 SIN CAMBIOS - {ahora}";
                Console.WriteLine(mensaje);
                RegistrarLog(mensaje);
                return;
            }

            var mensajeActualizando = $"🟢 ACTUALIZANDO COTIZACIONES - {ahora}";
            Console.WriteLine(mensajeActualizando);
            RegistrarLog(mensajeActualizando);

            var cmdDolar = new SqlCommand("INSERT INTO CotizacionesDolarJson (JsonData) VALUES (@Json)", conn);
            cmdDolar.Parameters.AddWithValue("@Json", jsonDolar);
            await cmdDolar.ExecuteNonQueryAsync();

            var cmdOtros = new SqlCommand("INSERT INTO CotizacionesOtrosJson (JsonData) VALUES (@Json)", conn);
            cmdOtros.Parameters.AddWithValue("@Json", jsonOtros);
            await cmdOtros.ExecuteNonQueryAsync();
        }

        static async Task<string?> ObtenerUltimoJson(SqlConnection conn, string tabla)
        {
            var cmd = new SqlCommand($"SELECT TOP 1 JsonData FROM {tabla} ORDER BY FechaEjecucion DESC", conn);
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        static void RegistrarLog(string mensaje)
        {
            var carpetaLogs = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(carpetaLogs); // ✅ Crea la carpeta si no existe

            var nombreArchivo = $"{DateTime.Now:yyyy-MM-dd}.log";
            var rutaLog = Path.Combine(carpetaLogs, nombreArchivo);

            var entrada = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
            File.AppendAllText(rutaLog, entrada + Environment.NewLine);
        }

        static async Task<List<ProyeccionDolar>> GenerarProyeccionesAsync(SqlConnection conn)
        {
            var historico = await ObtenerHistoricoAgrupadoAsync(conn);
            var proyecciones = new List<ProyeccionDolar>();

            foreach (var kvp in historico)
            {
                var nombre = kvp.Key;
                var datos = kvp.Value;
                if (datos.Count < 5) continue;

                var icono = nombre.ToLower() switch
                {
                    var n when n.Contains("blue") => "bi-cash-coin",
                    var n when n.Contains("oficial") => "bi-bank",
                    var n when n.Contains("mep") => "bi-graph-up",
                    var n when n.Contains("cripto") => "bi-currency-bitcoin",
                    _ => "bi-currency-exchange"
                };

                var actual = datos.Last().Compra;
                var semana = datos.Skip(Math.Max(0, datos.Count - 7)).Average(x => x.Compra);
                var mes = datos.Skip(Math.Max(0, datos.Count - 30)).Average(x => x.Compra);

                var variacionSemana = actual - semana;
                var variacionMes = actual - mes;
                var confianza = Math.Min(1.0, datos.Count / 30.0);

                proyecciones.Add(new ProyeccionDolar
                {
                    Nombre = nombre,
                    Icono = icono,
                    ValorSemana = Math.Round(actual + variacionSemana, 2),
                    ValorMes = Math.Round(actual + variacionMes, 2),
                    Justificacion = $"Basado en {datos.Count} registros. Δ semana: {variacionSemana:+0.00;-0.00}, Δ mes: {variacionMes:+0.00;-0.00}.",
                    Confianza = Math.Round(confianza, 2)
                });
            }

            return proyecciones;
        }

        static async Task GuardarProyeccionesSiCambianAsync(SqlConnection conn, List<ProyeccionDolar> nuevas)
        {
            var jsonNuevo = JsonSerializer.Serialize(nuevas);
            var cmd = new SqlCommand("SELECT TOP 1 JsonData FROM ProyeccionesDolarJson ORDER BY FechaEjecucion DESC", conn);
            var jsonViejo = (string?)await cmd.ExecuteScalarAsync();

            var ahora = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            if (jsonViejo == jsonNuevo)
            {
                var mensaje = $"🟡 PROYECCIONES SIN CAMBIOS - {ahora}";
                Console.WriteLine(mensaje);
                RegistrarLog(mensaje);
                return;
            }

            var insertCmd = new SqlCommand("INSERT INTO ProyeccionesDolarJson (JsonData) VALUES (@Json)", conn);
            insertCmd.Parameters.AddWithValue("@Json", jsonNuevo);
            await insertCmd.ExecuteNonQueryAsync();

            var mensajeActualizando = $"🧠 PROYECCIONES ACTUALIZADAS - {ahora}";
            Console.WriteLine(mensajeActualizando);
            RegistrarLog(mensajeActualizando);
        }

        static async Task<Dictionary<string, List<(DateTime Fecha, decimal Compra)>>> ObtenerHistoricoAgrupadoAsync(SqlConnection conn)
        {
            var resultado = new Dictionary<string, List<(DateTime, decimal)>>();
            var cmd = new SqlCommand("SELECT TOP 30 FechaEjecucion, JsonData FROM CotizacionesDolarJson ORDER BY FechaEjecucion DESC", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fecha = reader.GetDateTime(0);
                var json = reader.GetString(1);
                var cotizaciones = JsonSerializer.Deserialize<List<ApiCotizacion>>(json);

                foreach (var c in cotizaciones ?? Enumerable.Empty<ApiCotizacion>())
                {
                    if (!resultado.TryGetValue(c.Nombre, out var lista))
                    {
                        lista = new();
                        resultado[c.Nombre] = lista;
                    }

                    lista.Add((fecha, c.Compra));
                }
            }

            foreach (var key in resultado.Keys.ToList())
                resultado[key] = [.. resultado[key].OrderBy(x => x.Item1)];

            return resultado;
        }
    }
}