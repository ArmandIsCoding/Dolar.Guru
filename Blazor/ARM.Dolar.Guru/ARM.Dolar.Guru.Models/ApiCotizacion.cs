namespace ARM.Dolar.Guru.Models
{
    public record Cotizacion(string Nombre, decimal Compra, decimal Venta, DateTime FechaActualizacion);
    public class ApiCotizacion
    {
        public string Moneda { get; set; } = string.Empty;
        public string Casa { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public decimal Compra { get; set; }
        public decimal Venta { get; set; }
        public string FechaActualizacion { get; set; } = string.Empty;
    }
}
