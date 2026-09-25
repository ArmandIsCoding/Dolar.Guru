using ARM.Dolar.Guru.Models;

namespace ARM.Dolar.Guru.Services;

public sealed class NewsService(GuruDatabase database)
{
    public Task<List<NewsItem>> ObtenerUltimasNoticiasAsync(int cantidad = 10)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Titulo, Resumen, Url, Fuente, FechaPublicacion FROM News
            ORDER BY FechaPublicacion DESC, Id DESC LIMIT $count
            """;
        command.Parameters.AddWithValue("$count", Math.Clamp(cantidad, 1, 100));
        using var reader = command.ExecuteReader();
        var items = new List<NewsItem>();
        while (reader.Read())
            items.Add(new NewsItem
            {
                Id = reader.GetInt32(0), Titulo = reader.GetString(1),
                Resumen = reader.IsDBNull(2) ? null : reader.GetString(2),
                Url = reader.GetString(3), Fuente = reader.IsDBNull(4) ? null : reader.GetString(4),
                FechaPublicacion = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)
            });
        return Task.FromResult(items);
    }
}
