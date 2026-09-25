using System.Text.Json;
using ARM.Dolar.Guru.Services;

namespace ARM.Dolar.Guru.Sync;

/// <summary>Imports the export in docs/export-sqlserver.sql, atomically and without a SQL Server runtime dependency.</summary>
public static class LegacyImport
{
    public static async Task RunAsync(GuruDatabase database, string file)
    {
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file));
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var imported = 0;
        foreach (var table in GuruDatabase.SnapshotTables)
        {
            if (!json.RootElement.TryGetProperty(table, out var rows)) continue;
            foreach (var row in rows.EnumerateArray())
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"""
                    INSERT INTO {table}(FechaEjecucion,JsonData)
                    SELECT $date,$json WHERE NOT EXISTS
                    (SELECT 1 FROM {table} WHERE FechaEjecucion=$date AND JsonData=$json)
                    """;
                command.Parameters.AddWithValue("$date", row.GetProperty("FechaEjecucion").GetDateTime());
                command.Parameters.AddWithValue("$json", row.GetProperty("JsonData").GetString()!);
                imported += command.ExecuteNonQuery();
            }
        }
        if (json.RootElement.TryGetProperty("News", out var news))
            foreach (var row in news.EnumerateArray())
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO News(Titulo,Resumen,Url,Fuente,FechaPublicacion)
                    VALUES($title,$summary,$url,$source,$date) ON CONFLICT(Url) DO NOTHING
                    """;
                command.Parameters.AddWithValue("$title", row.GetProperty("Titulo").GetString()!);
                command.Parameters.AddWithValue("$summary", row.TryGetProperty("Resumen", out var summary) ? (object?)summary.GetString() ?? DBNull.Value : DBNull.Value);
                command.Parameters.AddWithValue("$url", row.GetProperty("Url").GetString()!);
                command.Parameters.AddWithValue("$source", row.TryGetProperty("Fuente", out var source) ? (object?)source.GetString() ?? DBNull.Value : DBNull.Value);
                command.Parameters.AddWithValue("$date", row.GetProperty("FechaPublicacion").GetDateTime());
                imported += command.ExecuteNonQuery();
            }
        transaction.Commit();
        Console.WriteLine($"Importación completada: {imported} registros nuevos.");
    }
}
