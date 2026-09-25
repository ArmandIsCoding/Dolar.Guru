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
    }
}
