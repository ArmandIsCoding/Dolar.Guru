using ARM.Mesa.Bursatil.Models;
using System.Security.Cryptography;
using System.Text;

namespace ARM.Mesa.Bursatil.Services;

public sealed class NewsService(MarketDatabase database)
{
    public Task<List<NewsItem>> ObtenerUltimasNoticiasAsync(int cantidad = 10)
    {
        var limit = Math.Clamp(cantidad, 1, 100);
        var items = Read(limit, sinceUtc: null, forAi: false, untilUtc: DateTime.UtcNow.AddMinutes(15));
        if (items.Count == 0) return Task.FromResult(items);
        // Diversify the latest news cycle, not months-old articles merely to fill a quota.
        var newest = items.Max(item => item.FechaPublicacion);
        var recent = items.Where(item => item.FechaPublicacion >= newest.AddHours(-48));
        return Task.FromResult(Balanced(recent, limit, limit, limit));
    }

    /// <summary>Only explicitly licensed/approved feed text is eligible for AI transmission.</summary>
    public List<NewsItem> GetBriefingCandidates(DateTime sinceUtc, int maxItems = 40,
        int maxPerPublisher = 8, int maxInternational = 4, DateTime? untilUtc = null)
    {
        var limit = Math.Clamp(maxItems, 1, 100);
        var items = Read(Math.Clamp(maxPerPublisher, 1, limit), sinceUtc.ToUniversalTime(), forAi: true,
                untilUtc: untilUtc ?? DateTime.UtcNow)
            .Where(item => Math.Max(item.ContentText.Length, item.Resumen?.Length ?? 0) >= 100);
        return Balanced(items, limit, Math.Clamp(maxPerPublisher, 1, limit), Math.Clamp(maxInternational, 0, limit));
    }

    private List<NewsItem> Read(int perPublisher, DateTime? sinceUtc, bool forAi, DateTime untilUtc)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH Ranked AS (
                SELECT *, ROW_NUMBER() OVER (
                    PARTITION BY COALESCE(NULLIF(PublisherGroup,''), NULLIF(Fuente,''), Url)
                    ORDER BY julianday(FechaPublicacion) DESC, Id DESC) AS PublisherRank
                FROM News WHERE ($since IS NULL OR julianday(FechaPublicacion) >= julianday($since))
                    AND julianday(FechaPublicacion) <= julianday($until) AND ($ai=0 OR AllowAiUse=1)
            )
            SELECT Id,Titulo,Resumen,COALESCE(NULLIF(CanonicalUrl,''),Url),Fuente,FechaPublicacion,
                SourceId,PublisherGroup,IsInternational,AllowAiUse,FetchedAtUtc,ContentHash,ContentText,ContentKind
            FROM Ranked WHERE PublisherRank <= $count
            ORDER BY julianday(FechaPublicacion) DESC, Id DESC LIMIT 2000
            """;
        command.Parameters.AddWithValue("$count", perPublisher);
        command.Parameters.AddWithValue("$since", sinceUtc is null ? DBNull.Value : sinceUtc.Value);
        command.Parameters.AddWithValue("$until", untilUtc.ToUniversalTime());
        command.Parameters.AddWithValue("$ai", forAi ? 1 : 0);
        using var reader = command.ExecuteReader();
        var items = new List<NewsItem>();
        while (reader.Read())
        {
            var safeUrl = NewsUrl.Canonicalize(reader.GetString(3));
            if (safeUrl is null) continue;
            items.Add(new NewsItem
            {
                Id = reader.GetInt32(0), Titulo = reader.GetString(1),
                Resumen = reader.IsDBNull(2) ? null : reader.GetString(2),
                Url = safeUrl, Fuente = reader.IsDBNull(4) ? null : reader.GetString(4),
                FechaPublicacion = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                SourceId = reader.GetString(6), PublisherGroup = reader.GetString(7),
                IsInternational = reader.GetInt32(8) != 0, AllowAiUse = reader.GetInt32(9) != 0,
                FetchedAtUtc = reader.IsDBNull(10) ? null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc),
                ContentHash = reader.GetString(11), ContentText = reader.GetString(12), ContentKind = reader.GetString(13)
            });
        }
        return items;
    }

    private static List<NewsItem> Balanced(IEnumerable<NewsItem> items, int limit, int maxPerPublisher, int maxInternational)
    {
        var groups = items.OrderByDescending(item => item.FechaPublicacion)
            .DistinctBy(item => item.Url, StringComparer.Ordinal)
            .GroupBy(item => string.IsNullOrWhiteSpace(item.PublisherGroup)
                ? item.Fuente ?? new Uri(item.Url!).Host : item.PublisherGroup)
            .Select(group => new Queue<NewsItem>(group.Take(maxPerPublisher))).ToList();
        var selected = new List<NewsItem>();
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var international = 0;
        while (selected.Count < limit && groups.Any(group => group.Count > 0))
        {
            foreach (var group in groups.Where(group => group.Count > 0)
                         .OrderByDescending(group => group.Peek().FechaPublicacion).ToArray())
            {
                var item = group.Dequeue();
                if (item.IsInternational && international >= maxInternational) continue;
                // The same syndicated body with a different headline is still one account,
                // not independent corroboration. ContentHash itself also tracks title edits.
                var evidence = item.ContentText.Length >= 100 ? item.ContentText : item.Resumen;
                var evidenceHash = evidence?.Length >= 100
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))) : item.ContentHash;
                if (evidenceHash.Length > 0 && !hashes.Add(evidenceHash)) continue;
                selected.Add(item);
                if (item.IsInternational) international++;
                if (selected.Count == limit) break;
            }
        }
        return selected;
    }
}
