using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARM.Mesa.Bursatil.Models;
using ARM.Mesa.Bursatil.Services;

namespace ARM.Mesa.Bursatil.Sync;

public sealed class BriefingGenerator(MarketDatabase database, BriefingOptions options,
    IBriefingAiClient client, Action<string>? report = null)
{
    public async Task<long?> GenerateAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) { report?.Invoke("Síntesis IA desactivada; no se llama a ningún proveedor."); return null; }
        options.Validate();
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.");
        var slot = GetSlot(nowUtc, options.ScheduleHours);
        if (slot is null) { report?.Invoke("Todavía no hay una franja editorial habilitada hoy."); return null; }
        var news = new NewsService(database).GetBriefingCandidates(nowUtc.AddHours(-options.LookbackHours),
            options.MaxArticles, options.MaxPerPublisher, Math.Max(1, options.MaxArticles / 10), untilUtc: nowUtc);
        var sources = news.Where(n => n.FechaPublicacion <= nowUtc && BriefingService.SafeUrl(n.Url))
            .Select(n => new BriefingSource
            {
                NewsId = n.Id, Title = n.Titulo, Url = n.Url!, SourceName = n.Fuente ?? n.SourceId,
                PublisherKey = n.PublisherGroup, PublishedAtUtc = n.FechaPublicacion,
                IsInternational = n.IsInternational,
                EvidenceText = Limit(string.IsNullOrWhiteSpace(n.ContentText) ? n.Resumen ?? "" : n.ContentText, 4000)
            }).Where(s => s.EvidenceText.Length >= 100).ToList();
        if (sources.Count < 4 || sources.Select(s => s.PublisherKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() < options.MinPublishers)
        {
            report?.Invoke("Sin cobertura suficiente: se necesitan 4 noticias con texto autorizado y al menos 3 grupos editoriales recientes. Se conserva la edición anterior.");
            return null;
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            sources.OrderBy(s => s.NewsId).Select(s => new { s.NewsId, s.Title, s.EvidenceText, s.Url, s.PublisherKey, s.PublishedAtUtc })))));
        var inputCap = client.EstimateMaxInputTokens(sources, nowUtc);
        var outputCap = client.EstimateMaxOutputTokens();
        if (inputCap <= 0 || outputCap <= 0) throw new InvalidOperationException("Reserva de tokens inválida.");
        var reservation = decimal.Ceiling((inputCap * options.InputUsdPerMillion + outputCap * options.OutputUsdPerMillion) / 1_000_000m * 1_000_000m) / 1_000_000m;
        var service = new BriefingService(database);
        if (!service.TryReserve(slot, fingerprint, nowUtc, reservation, options.MonthlyBudgetUsd, options.Provider, options.Model, out var reason))
        {
            report?.Invoke(reason);
            return null;
        }
        try
        {
            var result = await client.GenerateAsync(sources, nowUtc, cancellationToken);
            if (result.InputTokens < 0 || result.OutputTokens < 0) throw new InvalidDataException("Uso de tokens inválido.");
            var edition = new NewsBriefing
            {
                CreatedAtUtc = nowUtc, CoverageStartUtc = sources.Min(s => s.PublishedAtUtc), CoverageEndUtc = nowUtc,
                Provider = options.Provider, Model = options.Model, Sources = sources,
                Summary = result.Summary, Topics = result.Topics
            };
            var id = service.SaveDraft(edition);
            var cost = (result.InputTokens * options.InputUsdPerMillion + result.OutputTokens * options.OutputUsdPerMillion) / 1_000_000m;
            service.CompleteRun(slot, id, result.InputTokens, result.OutputTokens, cost);
            report?.Invoke($"Borrador {id} guardado para revisión, NO publicado. Tokens={result.InputTokens}/{result.OutputTokens}; coste calculado USD {cost.ToString("F6", CultureInfo.InvariantCulture)}.");
            return id;
        }
        catch
        {
            service.FailRun(slot);
            // Do not log provider payloads, article contents or credentials.
            report?.Invoke("La síntesis falló. Se conserva la edición publicada y la reserva de coste; no se reintenta esta franja.");
            throw;
        }
    }

    public static string? GetSlot(DateTime nowUtc, int[] scheduleHours)
    {
        // Argentina has UTC-03 year-round. Fixed offset makes Windows/Linux scheduling identical.
        var local = new DateTimeOffset(nowUtc).ToOffset(TimeSpan.FromHours(-3));
        var hours = scheduleHours.Where(h => h <= local.Hour).OrderDescending().ToArray();
        return hours.Length == 0 ? null : $"{local:yyyy-MM-dd}/{hours[0]:D2}";
    }

    private static string Limit(string text, int limit) => text.Length > limit ? text[..limit] : text;
}
