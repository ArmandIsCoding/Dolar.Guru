using System.Globalization;
using System.Net;
using ARM.Mesa.Bursatil.Services;
using ARM.Mesa.Bursatil.Sync;

public static class NewsIngestionChecks
{
    public static async Task RunAsync(string temporaryDirectory)
    {
        var db = new MarketDatabase(Path.Combine(temporaryDirectory, "news-ingestion.db"));
        // Simulate the deployed schema before new nullable/defaulted columns exist.
        using (var connection = db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE News(Id INTEGER PRIMARY KEY AUTOINCREMENT,Titulo TEXT NOT NULL,
                    Resumen TEXT,Url TEXT NOT NULL UNIQUE,Fuente TEXT,FechaPublicacion TEXT NOT NULL);
                INSERT INTO News(Titulo,Url,Fuente,FechaPublicacion) VALUES
                    ('Old title','https://alpha.example/article?utm_source=old','Old source',$date),
                    ('Unsafe link','javascript:alert(1)','Old source',$date);
                """;
            command.Parameters.AddWithValue("$date", DateTime.UtcNow.AddHours(-1));
            command.ExecuteNonQuery();
        }
        db.Initialize();
        NewsSchema.Initialize(db);
        NewsSchema.Initialize(db);
        var service = new NewsService(db);
        var migrated = (await service.ObtenerUltimasNoticiasAsync()).Single();
        Check(migrated.Id == 1 && migrated.Url == "https://alpha.example/article" && !migrated.AllowAiUse,
            "Additive news migration preserves IDs, canonicalizes old URLs and blocks unsafe legacy links");

        var handler = new FeedHttp();
        using var http = new HttpClient(handler);
        var news = new NewsFeedSynchronizer(db, http);
        var source = Source("alpha", allowAi: true);
        handler.Feed = Feed(
            Item("https://alpha.example/article?utm_campaign=new&amp;fbclid=123#section", "<b>Actualización argentina</b>",
                "<p>Resumen seguro &amp; claro.</p><script>LEAK_THIS_SCRIPT</script>",
                content: "<p>" + new string('x', 180) + "</p><style>LEAK_THIS_STYLE</style>"),
            Item("javascript:alert(1)", "URL insegura", "No usar"),
            Item("https://another.example/article", "Fuente ajena", "No atribuir"),
            Item("https://alpha.example/future", "Noticia futura", "No mostrar", DateTime.UtcNow.AddDays(2)),
            Item("https://alpha.example/nodate", "Noticia sin fecha", "No mostrar", includeDate: false));
        Check(await news.RunAsync(source) == 1, "RSS skips unsafe/cross-publisher links and missing/future dates");
        var first = (await service.ObtenerUltimasNoticiasAsync()).Single();
        Check(first.Id == 1 && first.Titulo == "Actualización argentina" && first.Resumen == "Resumen seguro & claro."
            && first.ContentKind == "rss-content" && first.ContentText.Length == 180
            && first.ContentHash.Length == 64 && first.FetchedAtUtc?.Kind == DateTimeKind.Utc
            && first.SourceId == "alpha" && first.PublisherGroup == "alpha" && first.Fuente == "ALPHA",
            "Feed text is sanitized and publisher identity, hash, RSS evidence and fetched time are stored");
        handler.Feed = Feed(Item("https://alpha.example/article?gclid=456", "Título corregido", new string('z', 200)));
        await news.RunAsync(source);
        var corrected = (await service.ObtenerUltimasNoticiasAsync()).Single();
        Check(corrected.Id == first.Id && corrected.ContentHash != first.ContentHash && corrected.Titulo == "Título corregido",
            "Corrections at the same canonical URL update metadata rather than insert duplicate articles");
        Check(service.GetBriefingCandidates(DateTime.UtcNow.AddDays(-1)).Count == 1,
            "Substantial approved RSS evidence is eligible for briefing candidates");
        news.ApplySourcePolicy([Source("alpha", allowAi: false)]);
        Check(service.GetBriefingCandidates(DateTime.UtcNow.AddDays(-1)).Count == 0,
            "Revoking source permission excludes already-cached evidence without needing another successful fetch");

        foreach (var id in new[] { "alpha", "beta", "gamma", "world" })
        {
            handler.Feed = Feed(Enumerable.Range(0, 5).Select(index => Item($"https://{id}.example/{index}",
                $"Noticia {id} {index}", $"Texto propio {id} {index}: " + new string((char)('a' + index), 150),
                DateTime.UtcNow.AddMinutes(-index - (id == "alpha" ? 0 : 10)))).ToArray());
            var currentSource = Source(id, allowAi: id != "gamma");
            currentSource.IsInternational = id == "world";
            await news.RunAsync(currentSource);
        }
        var display = await service.ObtenerUltimasNoticiasAsync(4);
        Check(display.Select(item => item.PublisherGroup).Distinct().Count() == 4,
            "Latest-feed display distributes the first slots across publishers, not publication volume");
        var candidates = service.GetBriefingCandidates(DateTime.UtcNow.AddDays(-1), maxItems: 20, maxPerPublisher: 2, maxInternational: 1);
        Check(candidates.Count == 5 && candidates.Count(item => item.IsInternational) == 1
            && candidates.All(item => item.SourceId != "gamma")
            && candidates.GroupBy(item => item.PublisherGroup).All(group => group.Count() <= 2),
            "AI candidates enforce approvals, publisher caps, international cap and freshness");
        Check(service.GetBriefingCandidates(DateTime.UtcNow.AddHours(1)).Count == 0,
            "Old evidence is not relabeled as current by fetch timestamps");

        var syndicated = "Texto propio alpha 0: " + new string('a', 150);
        handler.Feed = Feed(Item("https://beta.example/syndicated", "Titular distinto del mismo cable", syndicated));
        await news.RunAsync(Source("beta", allowAi: true));
        Check(service.GetBriefingCandidates(DateTime.UtcNow.AddDays(-1), maxPerPublisher: 20)
                .Count(item => item.ContentText == syndicated) == 1,
            "Identical syndicated evidence under different headlines is not counted as independent reporting");

        using (var connection = db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO News(Titulo,Resumen,Url,FechaPublicacion,SourceId,PublisherGroup,AllowAiUse)
                VALUES('Early ISO',$summary,'https://dates.example/early','2026-01-01T03:00:00.0000000Z','dates','dates',1),
                      ('In window',$summary,'https://dates.example/middle','2026-01-01 04:00:00','dates','dates',1),
                      ('After offset',$summary,'https://dates.example/after','2026-01-01T02:00:00-03:00','dates','dates',1);
                """;
            command.Parameters.AddWithValue("$summary", new string('m', 120));
            command.ExecuteNonQuery();
        }
        var historicalWindow = service.GetBriefingCandidates(new DateTime(2026, 1, 1, 3, 30, 0, DateTimeKind.Utc),
            untilUtc: new DateTime(2026, 1, 1, 4, 30, 0, DateTimeKind.Utc));
        Check(historicalWindow.Count == 1 && historicalWindow[0].Titulo == "In window",
            "Explicit UTC evidence window compares mixed SQLite, ISO and offset timestamps chronologically");

        var beforeFailure = (await service.ObtenerUltimasNoticiasAsync()).Select(item => item.Id).ToArray();
        handler.Feed = "<!DOCTYPE rss [<!ENTITY xxe SYSTEM 'file:///not-readable'>]><rss>&xxe;</rss>";
        var failed = false;
        try { await news.RunAsync(source); } catch (System.Xml.XmlException) { failed = true; }
        Check(failed && beforeFailure.SequenceEqual((await service.ObtenerUltimasNoticiasAsync()).Select(item => item.Id)),
            "Unsafe XML fails without replacing the last good news cache");
        handler.Status = HttpStatusCode.ServiceUnavailable;
        failed = false;
        try { await news.RunAsync(source); } catch (HttpRequestException) { failed = true; }
        Check(failed && handler.Requests.All(uri => uri.AbsolutePath == "/rss"),
            "Failed feed requests preserve cached news; article bodies are never fetched");
        Check(NewsUrl.Canonicalize("https://alpha.example/article?id=2&utm_source=x&z=1#fragment")
                == "https://alpha.example/article?id=2&z=1"
            && NewsUrl.Canonicalize("https://user:secret@alpha.example/article") is null
            && NewsUrl.Canonicalize("http://127.0.0.1/private") is null,
            "URL canonicalization preserves semantic query parameters and rejects credential/local links");
    }

    private static NewsFeedSource Source(string id, bool allowAi) => new()
    {
        Id = id, Name = id.ToUpperInvariant(), PublisherGroup = id,
        Url = $"https://{id}.example/rss", AllowAiUse = allowAi
    };

    private static string Feed(params string[] items) =>
        "<rss version=\"2.0\" xmlns:content=\"http://purl.org/rss/1.0/modules/content/\"><channel>"
        + "<title>Untrusted feed title</title><link>https://alpha.example</link><description>Fixture</description>"
        + string.Concat(items) + "</channel></rss>";

    private static string Item(string url, string title, string summary, DateTime? published = null,
        string? content = null, bool includeDate = true) =>
        "<item><title><![CDATA[" + title + "]]></title><link>" + url + "</link>"
        + "<description><![CDATA[" + summary + "]]></description>"
        + (includeDate ? "<pubDate>" + (published ?? DateTime.UtcNow.AddHours(-1)).ToString("R", CultureInfo.InvariantCulture) + "</pubDate>" : "")
        + (content is null ? "" : "<content:encoded><![CDATA[" + content + "]]></content:encoded>") + "</item>";

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class FeedHttp : HttpMessageHandler
    {
        public string Feed { get; set; } = "";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(Status)
                { Content = new StringContent(Feed, System.Text.Encoding.UTF8, "application/rss+xml") });
        }
    }
}
