namespace ARM.Mesa.Bursatil.Services;

/// <summary>Additive migration: historical News IDs and URLs remain intact.</summary>
public static class NewsSchema
{
    public static void Initialize(MarketDatabase database)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA table_info(News)";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = command.ExecuteReader())
            while (reader.Read()) columns.Add(reader.GetString(1));

        // Column names and types are literals, not configuration or feed input.
        (string Name, string Type)[] additions = [
            ("CanonicalUrl", "TEXT"), ("SourceId", "TEXT NOT NULL DEFAULT ''"),
            ("PublisherGroup", "TEXT NOT NULL DEFAULT ''"),
            ("IsInternational", "INTEGER NOT NULL DEFAULT 0"),
            ("AllowAiUse", "INTEGER NOT NULL DEFAULT 0"), ("FetchedAtUtc", "TEXT"),
            ("ContentHash", "TEXT NOT NULL DEFAULT ''"),
            ("ContentText", "TEXT NOT NULL DEFAULT ''"),
            ("ContentKind", "TEXT NOT NULL DEFAULT 'rss-summary'")
        ];
        foreach (var (name, type) in additions)
        {
            if (columns.Contains(name)) continue;
            command.CommandText = $"ALTER TABLE News ADD COLUMN {name} {type}";
            command.ExecuteNonQuery();
        }

        // Normalize old rows without deleting duplicates or altering IDs used elsewhere.
        command.CommandText = "SELECT Id, Url FROM News WHERE CanonicalUrl IS NULL";
        var oldRows = new List<(long Id, string Url)>();
        using (var reader = command.ExecuteReader())
            while (reader.Read()) oldRows.Add((reader.GetInt64(0), reader.GetString(1)));
        foreach (var (id, url) in oldRows)
        {
            command.CommandText = "UPDATE News SET CanonicalUrl=$url WHERE Id=$id";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$url", NewsUrl.Canonicalize(url) ?? "");
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        command.Parameters.Clear();
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_News_CanonicalUrl ON News(CanonicalUrl);
            CREATE INDEX IF NOT EXISTS IX_News_AiDate ON News(AllowAiUse, FechaPublicacion DESC);
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
