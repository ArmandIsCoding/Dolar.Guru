using System.Text.Json.Serialization;
using ARM.Dolar.Guru.Models; // Asegúrate de que el namespace sea el correcto

namespace ARM.Dolar.Guru.Models
{
    public class FuturoRavaRofex
    {
        [JsonPropertyName("especie")]
        public string Especie { get; set; } = string.Empty;

        // Las propiedades que pueden ser número o string ahora usan el convertidor
        [JsonPropertyName("ultimo")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string Ultimo { get; set; } = string.Empty;

        [JsonPropertyName("variacion")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string Variacion { get; set; } = string.Empty;

        [JsonPropertyName("varunit")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string VarUnit { get; set; } = string.Empty;

        [JsonPropertyName("apertura")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string Apertura { get; set; } = string.Empty;

        [JsonPropertyName("cantcompra")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string CantCompra { get; set; } = string.Empty;

        [JsonPropertyName("preciocompra")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string PrecioCompra { get; set; } = string.Empty;

        [JsonPropertyName("precioventa")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string PrecioVenta { get; set; } = string.Empty;

        [JsonPropertyName("cantventa")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string CantVenta { get; set; } = string.Empty;

        [JsonPropertyName("minimo")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string Minimo { get; set; } = string.Empty;

        [JsonPropertyName("maximo")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string Maximo { get; set; } = string.Empty;

        // --- El resto de las propiedades que no cambian ---

        [JsonPropertyName("simbolo")]
        public object? Simbolo { get; set; }

        [JsonPropertyName("plazo")]
        public object? Plazo { get; set; }

        [JsonPropertyName("varMTD")]
        public object? VarMTD { get; set; }

        [JsonPropertyName("varYTD")]
        public object? VarYTD { get; set; }

        [JsonPropertyName("anterior")]
        public object? Anterior { get; set; }

        [JsonPropertyName("volnominal")]
        public object? VolNominal { get; set; }

        [JsonPropertyName("volefectivo")]
        public object? VolEfectivo { get; set; }

        [JsonPropertyName("dias")]
        public object? Dias { get; set; }

        [JsonPropertyName("ultimo_tna")]
        public object? UltimoTna { get; set; }

        [JsonPropertyName("compra_tna")]
        public object? CompraTna { get; set; }

        [JsonPropertyName("venta_tna")]
        public object? VentaTna { get; set; }

        [JsonPropertyName("minimo_tna")]
        public object? MinimoTna { get; set; }

        [JsonPropertyName("maximo_tna")]
        public object? MaximoTna { get; set; }

        [JsonPropertyName("vencimiento")]
        public string Vencimiento { get; set; } = string.Empty;

        [JsonPropertyName("vwap")]
        public object? Vwap { get; set; }

        [JsonPropertyName("operaciones")]
        public object? Operaciones { get; set; }

        [JsonPropertyName("imbalance")]
        public object? Imbalance { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("tipo_orden")]
        public object? TipoOrden { get; set; }

        [JsonPropertyName("fecha")]
        public string Fecha { get; set; } = string.Empty;

        [JsonPropertyName("hora")]
        public string Hora { get; set; } = string.Empty;

        [JsonPropertyName("panel")]
        public string Panel { get; set; } = string.Empty;

        [JsonPropertyName("logo")]
        public object? Logo { get; set; }

        [JsonPropertyName("ratio")]
        public object? Ratio { get; set; }

        [JsonPropertyName("mercado")]
        public object? Mercado { get; set; }

        [JsonPropertyName("volpromedio")]
        public int VolPromedio { get; set; }

        [JsonPropertyName("volporcentual")]
        public int VolPorcentual { get; set; }
    }
}
