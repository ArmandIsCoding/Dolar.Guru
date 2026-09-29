using System.Net;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ARM.Mesa.Bursatil.Services;
using HtmlAgilityPack;

namespace ARM.Mesa.Bursatil.Sync;

/// <summary>Fetches only configured RSS feeds. It never follows article links or paywalls.</summary>
public sealed partial class NewsFeedSynchronizer(MarketDatabase database, HttpClient http)
{
    private const int MaximumFeedBytes = 5_000_000;

    public void ApplySourcePolicy(IEnumerable<NewsFeedSource> sources)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Fail closed also for sources removed from configuration or temporarily unreachable.
        command.CommandText = "UPDATE News SET AllowAiUse=0 WHERE AllowAiUse<>0";
        command.ExecuteNonQuery();
        foreach (var source in sources.Where(source => source.Enabled && source.AllowAiUse))
        {
            command.CommandText = "UPDATE News SET AllowAiUse=1 WHERE SourceId=$id";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", source.Id);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public async Task<int> RunAsync(NewsFeedSource source, int maxItems = 50, CancellationToken cancellationToken = default)
    {
        if (!source.Enabled) return 0;
        if (!SourceIdentifier().IsMatch(source.Id) || !SourceIdentifier().IsMatch(source.PublisherGroup) ||
            string.IsNullOrWhiteSpace(source.Name) || source.Name.Length > 120 ||
            NewsUrl.Canonicalize(source.Url) is null)
            throw new InvalidDataException("Fuente RSS inválida: revise Id, PublisherGroup, Name y URL pública.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var fetchToken = timeout.Token;
        using var response = await http.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, fetchToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumFeedBytes)
            throw new InvalidDataException("El RSS excede el tamaño permitido.");
        using var stream = await response.Content.ReadAsStreamAsync(fetchToken);
        using var payload = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, fetchToken)) > 0)
        {
            if (payload.Length + read > MaximumFeedBytes)
                throw new InvalidDataException("El RSS excede el tamaño permitido.");
            await payload.WriteAsync(buffer.AsMemory(0, read), fetchToken);
        }
        payload.Position = 0;
        using var reader = XmlReader.Create(payload, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaximumFeedBytes, MaxCharactersFromEntities = 0
        });
        var feed = SyndicationFeed.Load(reader) ?? throw new InvalidDataException("RSS vacío o incompatible.");
        var fetchedAt = DateTime.UtcNow;
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var count = 0;
        foreach (var item in feed.Items.Take(Math.Clamp(maxItems, 1, 100)))
        {
            var link = item.Links.FirstOrDefault(link => link.RelationshipType is "alternate" or null or "")?.Uri
                ?? item.Links.FirstOrDefault()?.Uri;
            var canonical = NewsUrl.Canonicalize(link?.ToString());
            var title = PlainText(item.Title?.Text, 300);
            if (canonical is null || title.Length < 5 || !SamePublisherHost(canonical, source.Url)) continue;
            var published = item.PublishDate == DateTimeOffset.MinValue ? item.LastUpdatedTime : item.PublishDate;
            // Missing/future timestamps are not silently presented as fresh journalism.
            if (published == DateTimeOffset.MinValue || published.UtcDateTime < new DateTime(2000, 1, 1) ||
                published.UtcDateTime > fetchedAt.AddMinutes(15)) continue;
            var summary = PlainText(item.Summary?.Text, 1600);
            var feedContent = item.Content is TextSyndicationContent content ? content.Text : null;
            var encoded = item.ElementExtensions.FirstOrDefault(extension =>
                extension.OuterName == "encoded" && extension.OuterNamespace == "http://purl.org/rss/1.0/modules/content/");
            if (encoded is not null)
            {
                using var contentReader = encoded.GetReader();
                feedContent = contentReader.ReadElementContentAsString();
            }
            var contentText = PlainText(feedContent, 12000);
            var contentKind = contentText.Length > summary.Length ? "rss-content" : "rss-summary";
            if (contentKind == "rss-summary") contentText = summary;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title + "\n" + summary + "\n" + contentText)));

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT MIN(Id) FROM News WHERE CanonicalUrl=$url OR Url=$url";
            command.Parameters.AddWithValue("$url", canonical);
            var existingId = command.ExecuteScalar();
            command.CommandText = existingId is null or DBNull
                ? """
                  INSERT INTO News(Titulo,Resumen,Url,CanonicalUrl,Fuente,FechaPublicacion,SourceId,PublisherGroup,
                      IsInternational,AllowAiUse,FetchedAtUtc,ContentHash,ContentText,ContentKind)
                  VALUES($title,$summary,$url,$url,$source,$date,$sourceId,$publisher,$international,$ai,$fetched,$hash,$content,$kind)
                  """
                : """
                  UPDATE News SET Titulo=$title,Resumen=$summary,CanonicalUrl=$url,Fuente=$source,
                      FechaPublicacion=$date,SourceId=$sourceId,PublisherGroup=$publisher,
                      IsInternational=$international,AllowAiUse=$ai,FetchedAtUtc=$fetched,
                      ContentHash=$hash,ContentText=$content,ContentKind=$kind WHERE Id=$id
                  """;
            if (existingId is not null and not DBNull) command.Parameters.AddWithValue("$id", existingId);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$summary", summary);
            command.Parameters.AddWithValue("$source", source.Name);
            command.Parameters.AddWithValue("$date", published.UtcDateTime);
            command.Parameters.AddWithValue("$sourceId", source.Id);
            command.Parameters.AddWithValue("$publisher", source.PublisherGroup);
            command.Parameters.AddWithValue("$international", source.IsInternational ? 1 : 0);
            command.Parameters.AddWithValue("$ai", source.AllowAiUse ? 1 : 0);
            command.Parameters.AddWithValue("$fetched", fetchedAt);
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$content", contentText);
            command.Parameters.AddWithValue("$kind", contentKind);
            command.ExecuteNonQuery();
            count++;
        }
        transaction.Commit();
        return count;
    }

    private static bool SamePublisherHost(string articleUrl, string feedUrl)
    {
        static string Host(string value)
        {
            var host = new Uri(value).IdnHost;
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        }
        var article = Host(articleUrl);
        var feed = Host(feedUrl);
        return article.Equals(feed, StringComparison.OrdinalIgnoreCase) ||
            article.EndsWith("." + feed, StringComparison.OrdinalIgnoreCase);
    }

    private static string PlainText(string? html, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.SelectNodes("//script|//style|//iframe|//noscript")?.ToArray() ?? []) node.Remove();
        var text = WebUtility.HtmlDecode(string.Join(" ", document.DocumentNode.DescendantsAndSelf()
            .OfType<HtmlTextNode>().Select(node => node.Text)));
        text = Whitespace().Replace(text, " ").Trim();
        text = new string(text.Where(character => !char.IsControl(character)).ToArray());
        return text.Length > maxLength ? text[..maxLength].TrimEnd() : text;
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceIdentifier();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
