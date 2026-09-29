namespace ARM.Mesa.Bursatil.Models
{
    public class NewsItem
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Resumen { get; set; }
        public string? Url { get; set; }
        public string? Fuente { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public string PublisherGroup { get; set; } = string.Empty;
        public bool IsInternational { get; set; }
        public bool AllowAiUse { get; set; }
        public DateTime? FetchedAtUtc { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        // Text supplied by the RSS publisher, never a scraped/paywalled article.
        public string ContentText { get; set; } = string.Empty;
        public string ContentKind { get; set; } = "rss-summary";
    }
}
