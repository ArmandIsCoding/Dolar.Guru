using ARM.Dolar.Guru.Models;
using Ganss.Xss;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using System.Data.SqlClient;
using System.Globalization;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;



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

            Console.WriteLine("Descargando novedades RSS");
            await SincronizarNoticiasAsync(conn);

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

        public static async Task SincronizarNoticiasAsync(SqlConnection conn)
        {
            var feeds = new[]
            {
                // Clarín
                "https://www.clarin.com/rss/lo-ultimo/",
                "https://www.clarin.com/rss/politica/",
                "https://www.clarin.com/rss/mundo/",
                "https://www.clarin.com/rss/sociedad/",
                "https://www.clarin.com/rss/policiales/",
                "https://www.clarin.com/rss/ciudades/",
                "https://www.clarin.com/rss/opinion/",
                "https://www.clarin.com/rss/cartas_al_pais/",
                "https://www.clarin.com/rss/cultura/",
                "https://www.clarin.com/rss/rural/",
                "https://www.clarin.com/rss/economia/",
                "https://www.clarin.com/rss/tecnologia/",
                "https://www.clarin.com/rss/internacional/",
                "https://www.clarin.com/rss/revista-enie/",
                "https://www.clarin.com/rss/viva/",
                "https://www.clarin.com/rss/br/",
                "https://www.clarin.com/rss/deportes/",
                "https://www.clarin.com/rss/espectaculos/tv/",
                "https://www.clarin.com/rss/espectaculos/cine/",
                "https://www.clarin.com/rss/espectaculos/musica/",
                "https://www.clarin.com/rss/espectaculos/teatro/",
                "https://www.clarin.com/rss/espectaculos/",
                "https://www.clarin.com/rss/autos/",
                "https://www.clarin.com/rss/buena-vida/",
                "https://www.clarin.com/rss/viajes/",
                "https://www.clarin.com/rss/arq/",

                // Perfil
                "https://www.perfil.com/feed",

                // Buenos Aires Times (en inglés)
                "https://batimes.com.ar/feed",

                // Diario Río Negro
                "https://www.rionegro.com.ar/feed/",

                // Crónica
                "https://www.cronica.com.ar/rss/feed.html",

                // Minuto Uno
                "https://www.minutouno.com/rss",

                // Diario Uno (Mendoza)
                "https://www.diariouno.com.ar/rss",

                // El Tribuno (Salta)
                "https://www.eltribuno.com/salta/rss",

                // Ámbito (solo si vuelve a estar online)
                // "https://www.ambito.com/contenidos/economia.xml",

                "https://www.santafe.gov.ar/rss_noticias.php",
                "https://www.santafe.gov.ar/index.php/guia/portal_compras?pagina=rss",

                "http://www.ole.com.ar/rss/ultimas-noticias/",
                "http://www.ole.com.ar/rss/equipos/",
                "http://www.ole.com.ar/rss/boca-juniors/",
                "http://www.ole.com.ar/rss/river-plate/",
                "http://www.ole.com.ar/rss/san-lorenzo/",
                "http://www.ole.com.ar/rss/racing/",
                "http://www.ole.com.ar/rss/independiente/",
                "http://www.ole.com.ar/rss/huracan/",
                "http://www.ole.com.ar/rss/velez/",
                "http://www.ole.com.ar/rss/futbol-primera/",
                "http://www.ole.com.ar/rss/futbol-ascenso/",
                "http://www.ole.com.ar/rss/futbol-ascenso/b-nacional/",
                "http://www.ole.com.ar/rss/futbol-ascenso/primera-b/",
                "http://www.ole.com.ar/rss/futbol-ascenso/primera-c/",
                "http://www.ole.com.ar/rss/futbol-ascenso/primera-d/",
                "http://www.ole.com.ar/rss/futbol-internacional/",
                "http://www.ole.com.ar/rss/futbol-internacional/libertadores",
                "http://www.ole.com.ar/rss/futbol-internacional/sudamericana/",
                "http://www.ole.com.ar/rss/futbol-internacional/espana/",
                "http://www.ole.com.ar/rss/futbol-internacional/italia/",
                "http://www.ole.com.ar/rss/futbol-internacional/francia/",
                "http://www.ole.com.ar/rss/futbol-internacional/champions/",
                "http://www.ole.com.ar/rss/seleccion/",
                "http://www.ole.com.ar/rss/messi",
                "http://www.ole.com.ar/rss/nba/",
                "http://www.ole.com.ar/rss/basquet/",
                "http://www.ole.com.ar/rss/tenis/",
                "http://www.ole.com.ar/rss/autos/",
                "http://www.ole.com.ar/rss/rugby/",
                "http://www.ole.com.ar/rss/poli/",
                "http://www.ole.com.ar/rss/fuera-de-juego/",
                "http://www.ole.com.ar/rss/running/",
                "http://www.ole.com.ar/rss/artes-marciales/",
                "http://www.ole.com.ar/rss/boxeo/",
                "http://www.ole.com.ar/rss/opinion/",
                "http://www.ole.com.ar/rss/copa-argentina/",

                "http://www.lapoliticaonline.com.ar/files/rss/ultimasnoticias.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/politica.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/economia.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/ciudad.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/provincia.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/conurbano.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/mendoza.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/santafe.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/transporte.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/energía.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/judiciales.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/campo.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/entrevista.xml",
                "http://www.lapoliticaonline.com.ar/files/rss/medios.xml",

                "https://www.apfdigital.com.ar/rss/provinciales/",
                "https://www.apfdigital.com.ar/rss/politicas/",
                "https://www.apfdigital.com.ar/rss/policiales-y-judiciales/",
                "https://www.apfdigital.com.ar/rss/nacionales/",
                "https://www.apfdigital.com.ar/rss/internacionales/",
                "https://www.apfdigital.com.ar/rss/ciencia-y-tecnologia/",
                "https://www.apfdigital.com.ar/rss/municipales/",
                "https://www.apfdigital.com.ar/rss/primera-plana/",
                "https://www.apfdigital.com.ar/rss/interes-general/",
                "https://www.apfdigital.com.ar/rss/destacada/",
                "https://www.apfdigital.com.ar/rss/noticias/",
            };

            // Configurar el sanitizador
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.Add("b");
            sanitizer.AllowedTags.Add("strong");
            sanitizer.AllowedTags.Add("i");
            sanitizer.AllowedTags.Add("em");
            sanitizer.AllowedTags.Add("u");
            sanitizer.AllowedTags.Add("p");
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedTags.Add("ul");
            sanitizer.AllowedTags.Add("ol");
            sanitizer.AllowedTags.Add("li");
            //sanitizer.AllowedTags.Add("a");

            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("title");

            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");

            foreach (var feedUrl in feeds)
            {
                try
                {
                    var settings = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Ignore
                    };

                    using var reader = XmlReader.Create(feedUrl, settings);
                    var feed = SyndicationFeed.Load(reader);

                    foreach (var item in feed.Items)
                    {
                        var titulo = item.Title?.Text?.Trim();
                        var resumenOriginal = item.Summary?.Text?.Trim();
                        var resumenLimpio = string.IsNullOrWhiteSpace(resumenOriginal)
                            ? null
                            : sanitizer.Sanitize(resumenOriginal);

                        var link = item.Links.FirstOrDefault()?.Uri.ToString();
                        var fecha = item.PublishDate.UtcDateTime;

                        if (string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(link))
                            continue;

                        // Evitar duplicados por título + fecha
                        var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM News WHERE Titulo = @Titulo AND FechaPublicacion = @Fecha", conn);
                        checkCmd.Parameters.AddWithValue("@Titulo", titulo);
                        checkCmd.Parameters.AddWithValue("@Fecha", fecha);

                        var existe = (int)await checkCmd.ExecuteScalarAsync() > 0;
                        if (existe) continue;

                        var insertCmd = new SqlCommand(@"
                    INSERT INTO News (Titulo, Resumen, Url, Fuente, FechaPublicacion)
                    VALUES (@Titulo, @Resumen, @Url, @Fuente, @Fecha)", conn);

                        insertCmd.Parameters.AddWithValue("@Titulo", titulo);
                        insertCmd.Parameters.AddWithValue("@Resumen", (object?)resumenLimpio ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Url", link);
                        insertCmd.Parameters.AddWithValue("@Fuente", feed.Title?.Text ?? "");
                        insertCmd.Parameters.AddWithValue("@Fecha", fecha);

                        await insertCmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"🆕 Insertada: {titulo}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error leyendo {feedUrl}: {ex.Message}");
                }
            }
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
            // Verificamos si ya existe ese JSON exacto
            var checkCmd = new SqlCommand("SELECT COUNT(*) FROM FuturoRavaJson WHERE JsonData = @Json", conn);
            checkCmd.Parameters.AddWithValue("@Json", futurosJson);

            var existe = (int)await checkCmd.ExecuteScalarAsync() > 0;

            if (existe)
            {
                Console.WriteLine("⏩ Datos ya presentes en FuturoRavaJson, no se insertan duplicados.");
                return;
            }

            // Si no existe, insertamos
            var insertCmd = new SqlCommand(
                "INSERT INTO FuturoRavaJson (JsonData) VALUES (@Json)", conn);
            insertCmd.Parameters.AddWithValue("@Json", futurosJson);
            await insertCmd.ExecuteNonQueryAsync();
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