namespace ARM.Dolar.Guru.BaseBlazor.Models
{
    public static class TipoDivisaHelper
    {
        public static TipoDivisa Parse(string nombre)
        {
            return nombre switch
            {
                "Dólar" => TipoDivisa.Dolar,
                "Euro" => TipoDivisa.Euro,
                "Real Brasileño" => TipoDivisa.RealBrasileno,
                "Peso Chileno" => TipoDivisa.PesoChileno,
                "Peso Uruguayo" => TipoDivisa.PesoUruguayo,
                _ => TipoDivisa.Default
            };
        }
    }

    public static class TipoDolarHelper
    {
        public static TipoDolar Parse(string nombre)
        {
            return nombre switch
            {
                "Oficial" => TipoDolar.Oficial,
                "Blue" => TipoDolar.Blue,
                "Bolsa" => TipoDolar.Bolsa,
                "Contado con liquidación" => TipoDolar.ContadoConLiquidacion,
                "Tarjeta" => TipoDolar.Tarjeta,
                "Mayorista" => TipoDolar.Mayorista,
                "Cripto" => TipoDolar.Cripto,
                _ => TipoDolar.Default
            };
        }
    }

    public enum TipoDolar
    {
        Oficial,
        Blue,
        Bolsa,
        ContadoConLiquidacion,
        Tarjeta,
        Mayorista,
        Cripto,
        Default
    }

    public enum TipoDivisa
    {
        Dolar,
        Euro,
        RealBrasileno,
        PesoChileno,
        PesoUruguayo,
        Default
    }

    public record Estilo(string Color1, string Color2);

    public static class Estilos
    {
        public static readonly Dictionary<TipoDolar, Estilo> Dolar = new()
        {
            [TipoDolar.Oficial] = new("#1E90FF", "#00BFFF"),
            [TipoDolar.Blue] = new("#8A2BE2", "#6A5ACD"),
            [TipoDolar.Bolsa] = new("#3CB371", "#2E8B57"),
            [TipoDolar.ContadoConLiquidacion] = new("#FF6347", "#FF4500"),
            [TipoDolar.Tarjeta] = new("#FF69B4", "#FF1493"),
            [TipoDolar.Mayorista] = new("#4682B4", "#5F9EA0"),
            [TipoDolar.Cripto] = new("#FFD700", "#FFA500"),
            [TipoDolar.Default] = new("#343A40", "#6C757D")
        };

        public static readonly Dictionary<TipoDolar, string> IconosDolar = new()
        {
            [TipoDolar.Oficial] = "bi-bank",
            [TipoDolar.Blue] = "bi-briefcase-fill",
            [TipoDolar.Bolsa] = "bi-graph-up",
            [TipoDolar.ContadoConLiquidacion] = "bi-building",
            [TipoDolar.Tarjeta] = "bi-credit-card-fill",
            [TipoDolar.Mayorista] = "bi-diagram-3-fill",
            [TipoDolar.Cripto] = "bi-currency-bitcoin"
        };

        public static readonly Dictionary<TipoDivisa, Estilo> Otros = new()
        {
            [TipoDivisa.Dolar] = new("#007BFF", "#0056b3"),
            [TipoDivisa.Euro] = new("#6c757d", "#343a40"),
            [TipoDivisa.RealBrasileno] = new("#28a745", "#1e7e34"),
            [TipoDivisa.PesoChileno] = new("#17a2b8", "#117a8b"),
            [TipoDivisa.PesoUruguayo] = new("#ffc107", "#e0a800"),
            [TipoDivisa.Default] = new("#6c757d", "#adb5bd")
        };

        public static readonly Dictionary<TipoDivisa, string> IconosOtros = new()
        {
            [TipoDivisa.Dolar] = "bi-currency-dollar",
            [TipoDivisa.Euro] = "bi-currency-euro",
            [TipoDivisa.RealBrasileno] = "bi-currency-bitcoin",
            [TipoDivisa.PesoChileno] = "bi-cash-stack",
            [TipoDivisa.PesoUruguayo] = "bi-wallet2"
        };
    }
}