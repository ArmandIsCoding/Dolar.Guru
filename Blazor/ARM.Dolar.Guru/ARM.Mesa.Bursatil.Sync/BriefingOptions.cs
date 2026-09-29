namespace ARM.Mesa.Bursatil.Sync;

/// <summary>Non-secret generation options. The credential is read separately from private settings.</summary>
public sealed class BriefingOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "";
    public int MaxOutputTokens { get; set; } = 5000;
    public decimal MonthlyBudgetUsd { get; set; } = 10m;
    public decimal InputUsdPerMillion { get; set; }
    public decimal OutputUsdPerMillion { get; set; }
    public int[] ScheduleHours { get; set; } = [8, 12, 16, 20];
    public int MaxArticles { get; set; } = 24;
    public int MaxPerPublisher { get; set; } = 6;
    public int LookbackHours { get; set; } = 24;
    public int MinPublishers { get; set; } = 3;
    public int RequestTimeoutSeconds { get; set; } = 90;

    public void Validate()
    {
        if (!string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Provider, "Gemini", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Briefing:Provider debe ser OpenAI o Gemini.");
        if (string.IsNullOrWhiteSpace(Model) || Model.Length > 160
            || Model.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Briefing:Model requiere un identificador de modelo válido con salida JSON estructurada.");
        if (MonthlyBudgetUsd is <= 0 or > 1000 || InputUsdPerMillion is <= 0 or > 10000
            || OutputUsdPerMillion is <= 0 or > 10000)
            throw new ArgumentException("Briefing requiere un presupuesto positivo y las tarifas máximas vigentes del modelo, en USD por millón de tokens.");
        if (MaxOutputTokens is < 1000 or > 16000 || RequestTimeoutSeconds is < 10 or > 300)
            throw new ArgumentException("Briefing requiere MaxOutputTokens entre 1000 y 16000 y RequestTimeoutSeconds entre 10 y 300.");
        if (ScheduleHours is null || ScheduleHours.Length is < 1 or > 4 || ScheduleHours.Distinct().Count() != ScheduleHours.Length
            || ScheduleHours.Any(hour => hour is < 0 or > 23))
            throw new ArgumentException("Briefing:ScheduleHours requiere entre una y cuatro horas distintas, de 0 a 23.");
        if (MaxArticles is < 8 or > 48 || MaxPerPublisher is < 1 or > 12 || MaxPerPublisher > MaxArticles
            || LookbackHours is < 1 or > 48 || MinPublishers is < 3 or > 12 || MinPublishers > MaxArticles)
            throw new ArgumentException("Briefing tiene límites de selección de noticias inválidos.");
    }
}
