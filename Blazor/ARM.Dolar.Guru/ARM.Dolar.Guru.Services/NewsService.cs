namespace ARM.Dolar.Guru.Services
{
    using System.Data.SqlClient;
    using System.Text.Json;
    using ARM.Dolar.Guru.Models;
    using Microsoft.Data.SqlClient;

    public class NewsService
    {
        private readonly string connectionString = "Server=server;Database=DolarGuru;User Id=sa;Password=123456;TrustServerCertificate=True;";

        public NewsService()
        {
        }

        public async Task<List<NewsItem>> ObtenerUltimasNoticiasAsync(int cantidad = 10)
        {
            var lista = new List<NewsItem>();

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT TOP (@Cantidad) Id, Titulo, Resumen, Url, Fuente, FechaPublicacion
                FROM News
                ORDER BY FechaPublicacion DESC", conn);

            cmd.Parameters.AddWithValue("@Cantidad", cantidad);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new NewsItem
                {
                    Id = reader.GetInt32(0),
                    Titulo = reader.GetString(1),
                    Resumen = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Url = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Fuente = reader.IsDBNull(4) ? null : reader.GetString(4),
                    FechaPublicacion = reader.GetDateTime(5)
                });
            }

            return lista;
        }
    }
}
