namespace ARM.Mesa.Bursatil.Sync;

public sealed class NewsFeedOptions
{
    public int MaxItemsPerFeed { get; set; } = 50;
    public List<NewsFeedSource> Sources { get; set; } = DefaultSources();

    public static List<NewsFeedSource> DefaultSources() => [
        new() { Id = "clarin-economia", Name = "Clarín", PublisherGroup = "clarin", Url = "https://www.clarin.com/rss/economia/" },
        new() { Id = "perfil", Name = "Perfil", PublisherGroup = "perfil", Url = "https://www.perfil.com/feed" },
        new() { Id = "pagina12-economia", Name = "Página/12", PublisherGroup = "octubre", Url = "https://www.pagina12.com.ar/arc/outboundfeeds/rss/secciones/economia/notas" },
        new() { Id = "lanacion-economia", Name = "LA NACION", PublisherGroup = "lanacion", Url = "https://www.lanacion.com.ar/arc/outboundfeeds/rss/category/economia/?outputType=xml" },
        new() { Id = "ambito-economia", Name = "Ámbito", PublisherGroup = "indalo", Url = "https://www.ambito.com/rss/pages/economia.xml" },
        new() { Id = "chequeado", Name = "Chequeado", PublisherGroup = "chequeado", Url = "https://chequeado.com/feed/" }
    ];
}

public sealed class NewsFeedSource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string PublisherGroup { get; set; } = "";
    public bool IsInternational { get; set; }
    public bool Enabled { get; set; } = true;
    // An accessible RSS feed is not a license to send its text to an AI provider.
    // The operator must review the publisher's terms before explicitly enabling this.
    public bool AllowAiUse { get; set; }
}
