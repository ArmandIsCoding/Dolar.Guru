using System;

namespace ARM.Dolar.Guru.DTO
{
    public class DolarCotizacion
    {
        public string Moneda { get; set; }
        public string Casa { get; set; }
        public string Nombre { get; set; }
        public decimal Compra { get; set; }
        public decimal Venta { get; set; }
        public DateTime FechaActualizacion { get; set; }

        // Nueva propiedad para marcar cuando hay un cambio en los valores
        public bool Actualizado { get; set; } = false;

    }
}