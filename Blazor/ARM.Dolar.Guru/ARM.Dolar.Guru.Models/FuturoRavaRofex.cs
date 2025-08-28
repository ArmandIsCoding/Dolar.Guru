using System.Text.Json.Serialization;

namespace ARM.Dolar.Guru.Models
{
    public class FuturoRavaRofex
    {
        [JsonPropertyName("especie")]
        public string Especie { get; set; }

        [JsonPropertyName("simbolo")]
        public object Simbolo { get; set; }

        [JsonPropertyName("plazo")]
        public object Plazo { get; set; }

        [JsonPropertyName("ultimo")]
        public double Ultimo { get; set; }

        [JsonPropertyName("variacion")]
        public double Variacion { get; set; }

        [JsonPropertyName("varMTD")]
        public object VarMTD { get; set; }

        [JsonPropertyName("varYTD")]
        public object VarYTD { get; set; }

        [JsonPropertyName("varunit")]
        public double VarUnit { get; set; }

        [JsonPropertyName("anterior")]
        public object Anterior { get; set; }

        [JsonPropertyName("apertura")]
        public object Apertura { get; set; }

        [JsonPropertyName("cantcompra")]
        public object CantCompra { get; set; }

        [JsonPropertyName("preciocompra")]
        public object PrecioCompra { get; set; }

        [JsonPropertyName("precioventa")]
        public object PrecioVenta { get; set; }

        [JsonPropertyName("cantventa")]
        public object CantVenta { get; set; }

        [JsonPropertyName("minimo")]
        public object Minimo { get; set; }

        [JsonPropertyName("maximo")]
        public object Maximo { get; set; }

        [JsonPropertyName("volnominal")]
        public object VolNominal { get; set; }

        [JsonPropertyName("volefectivo")]
        public object VolEfectivo { get; set; }

        [JsonPropertyName("dias")]
        public object Dias { get; set; }

        [JsonPropertyName("ultimo_tna")]
        public object UltimoTna { get; set; }

        [JsonPropertyName("compra_tna")]
        public object CompraTna { get; set; }

        [JsonPropertyName("venta_tna")]
        public object VentaTna { get; set; }

        [JsonPropertyName("minimo_tna")]
        public object MinimoTna { get; set; }

        [JsonPropertyName("maximo_tna")]
        public object MaximoTna { get; set; }

        [JsonPropertyName("vencimiento")]
        public string Vencimiento { get; set; }

        [JsonPropertyName("vwap")]
        public object Vwap { get; set; }

        [JsonPropertyName("operaciones")]
        public object Operaciones { get; set; }

        [JsonPropertyName("imbalance")]
        public object Imbalance { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; }

        [JsonPropertyName("tipo_orden")]
        public object TipoOrden { get; set; }

        [JsonPropertyName("fecha")]
        public string Fecha { get; set; }

        [JsonPropertyName("hora")]
        public string Hora { get; set; }

        [JsonPropertyName("panel")]
        public string Panel { get; set; }

        [JsonPropertyName("logo")]
        public object Logo { get; set; }

        [JsonPropertyName("ratio")]
        public object Ratio { get; set; }

        [JsonPropertyName("mercado")]
        public object Mercado { get; set; }

        [JsonPropertyName("volpromedio")]
        public int VolPromedio { get; set; }

        [JsonPropertyName("volporcentual")]
        public int VolPorcentual { get; set; }
    }
}