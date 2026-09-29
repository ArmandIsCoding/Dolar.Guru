using System.Globalization;
using System.Text.Json;
using ARM.Mesa.Bursatil.Services;
using Microsoft.Extensions.Configuration;

namespace ARM.Mesa.Bursatil.Sync;

public static class BriefingCommands
{
    public static BriefingOptions ReadOptions(IConfiguration configuration) => new()
    {
        Enabled = bool.TryParse(configuration["Briefing:Enabled"], out var enabled) && enabled,
        Provider = configuration["Briefing:Provider"] ?? "OpenAI",
        Model = configuration["Briefing:Model"] ?? "",
        BillingMode = configuration["Briefing:BillingMode"] ?? "Paid",
        FreeTierConfirmed = bool.TryParse(configuration["Briefing:FreeTierConfirmed"], out var confirmed) && confirmed,
        MaxRequestsPer24Hours = ReadInt(configuration, "MaxRequestsPer24Hours", 4),
        MaxRequestsPerMonth = ReadInt(configuration, "MaxRequestsPerMonth", 124),
        MaxInputTokensPerRequest = ReadInt(configuration, "MaxInputTokensPerRequest", 200000),
        MaxOutputTokens = ReadInt(configuration, "MaxOutputTokens", 5000),
        MonthlyBudgetUsd = ReadDecimal(configuration, "MonthlyBudgetUsd", 10m),
        InputUsdPerMillion = ReadDecimal(configuration, "InputUsdPerMillion", 0m),
        OutputUsdPerMillion = ReadDecimal(configuration, "OutputUsdPerMillion", 0m),
        ScheduleHours = ReadHours(configuration),
        MaxArticles = ReadInt(configuration, "MaxArticles", 24),
        MaxPerPublisher = ReadInt(configuration, "MaxPerPublisher", 6),
        LookbackHours = ReadInt(configuration, "LookbackHours", 24),
        MinPublishers = ReadInt(configuration, "MinPublishers", 3),
        RequestTimeoutSeconds = ReadInt(configuration, "RequestTimeoutSeconds", 90)
    };

    private static int[] ReadHours(IConfiguration configuration)
    {
        var children = configuration.GetSection("Briefing:ScheduleHours").GetChildren().ToArray();
        return children.Length == 0 ? [8, 12, 16, 20]
            : children.Select(s => int.Parse(s.Value!, CultureInfo.InvariantCulture)).ToArray();
    }

    private static int ReadInt(IConfiguration config, string name, int fallback) => config[$"Briefing:{name}"] is { } value
        ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;
    private static decimal ReadDecimal(IConfiguration config, string name, decimal fallback) => config[$"Briefing:{name}"] is { } value
        ? decimal.Parse(value, CultureInfo.InvariantCulture) : fallback;

    public static async Task GenerateAsync(MarketDatabase database, IConfiguration configuration, ExecutionLog log)
    {
        var options = ReadOptions(configuration);
        if (!options.Enabled) { log.Info("El país en contexto: generación desactivada."); return; }
        options.Validate();
        var key = SyncConfiguration.ReadApiKey(configuration);
        log.Info(options.IsFreeTier
            ? "Síntesis Gemini en FreeTier: límites locales activos. La gratuidad depende del proyecto/modelo en Google; Sync no consulta ni cambia su facturación."
            : "Síntesis en modo Paid: presupuesto y tarifas locales activos.");
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds) };
        var client = new HttpBriefingAiClient(http, options, key);
        await new BriefingGenerator(database, options, client, log.Info).GenerateAsync(DateTime.UtcNow);
    }

    public static void CheckConfiguration(IConfiguration configuration, Action<string> report)
    {
        var options = ReadOptions(configuration);
        options.Validate();
        _ = SyncConfiguration.ReadApiKey(configuration);
        report($"Configuración local válida. Proveedor={options.Provider}; Modelo={options.Model}; Modo={options.BillingMode}; Enabled={options.Enabled}.");
        report($"Límites: {options.MaxRequestsPer24Hours} solicitudes/24 h, {options.MaxRequestsPerMonth}/mes UTC; entrada estimada máxima={options.MaxInputTokensPerRequest} tokens/solicitud.");
        report("Clave presente (no se muestra). Sin llamadas a la API. Esta comprobación no valida la clave, cuota, facturación ni disponibilidad del modelo en Google; tampoco autoriza fuentes.");
    }

    // Local administrator CLI, not an unauthenticated web endpoint.
    public static bool HandleReview(string[] args, MarketDatabase database)
    {
        var service = new BriefingService(database);
        if (args.Contains("--briefing-list"))
        {
            foreach (var edition in service.ListDrafts())
                Console.WriteLine($"Borrador {edition.Id} | {edition.CreatedAtUtc:O} | {edition.Topics.Count} temas | {edition.Provider}/{edition.Model}");
            if (service.GetLatestPublished() is { } current)
                Console.WriteLine($"Edición publicada: {current.Id} | cobertura hasta {current.CoverageEndUtc:O}");
            return true;
        }
        foreach (var operation in new[] { "--briefing-review", "--briefing-publish", "--briefing-reject" })
        {
            var index = Array.IndexOf(args, operation);
            if (index < 0) continue;
            if (index + 1 >= args.Length || !long.TryParse(args[index + 1], out var id) || id <= 0)
                throw new ArgumentException($"{operation} requiere un ID válido.");
            if (operation == "--briefing-review")
            {
                var edition = service.GetForReview(id) ?? throw new ArgumentException("No existe la edición.");
                Console.WriteLine("REVISIÓN EDITORIAL: comprobar cada afirmación, evidencia, fuente, cifras, atribución y fecha. La validación automática no garantiza veracidad ni pluralidad.");
                Console.WriteLine(JsonSerializer.Serialize(edition, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }
            else if (operation == "--briefing-reject")
            {
                service.Reject(id);
                Console.WriteLine($"Borrador {id} rechazado. Se conserva el historial.");
            }
            else
            {
                var reviewerIndex = Array.IndexOf(args, "--reviewed-by");
                if (!args.Contains("--confirm-reviewed") || reviewerIndex < 0 || reviewerIndex + 1 >= args.Length)
                    throw new ArgumentException("Primero revise --briefing-review ID. Para publicar indique --reviewed-by NOMBRE --confirm-reviewed.");
                service.Publish(id, args[reviewerIndex + 1], DateTime.UtcNow);
                Console.WriteLine($"Edición {id} publicada tras confirmación de revisión humana.");
            }
            return true;
        }
        return false;
    }
}
