using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ARM.Mesa.Bursatil.Models;
using Microsoft.Data.Sqlite;

namespace ARM.Mesa.Bursatil.Services;

/// <summary>Public reads only return explicitly reviewed, published editions.</summary>
public sealed class BriefingService(MarketDatabase database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Initialize(MarketDatabase database)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS NewsBriefings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedAtUtc TEXT NOT NULL,
                PublishedAtUtc TEXT, Status TEXT NOT NULL CHECK(Status IN ('draft','published','rejected')),
                ReviewedBy TEXT, JsonData TEXT NOT NULL CHECK(json_valid(JsonData)));
            CREATE INDEX IF NOT EXISTS IX_NewsBriefings_Public ON NewsBriefings(Status,PublishedAtUtc DESC,Id DESC);
            CREATE TABLE IF NOT EXISTS BriefingRuns (
                Slot TEXT PRIMARY KEY, Fingerprint TEXT NOT NULL, StartedAtUtc TEXT NOT NULL,
                Month TEXT NOT NULL, Status TEXT NOT NULL, ReservedUsd REAL NOT NULL,
                ChargedUsd REAL, InputTokens INTEGER, OutputTokens INTEGER,
                Provider TEXT NOT NULL, Model TEXT NOT NULL, EditionId INTEGER, Error TEXT);
            CREATE INDEX IF NOT EXISTS IX_BriefingRuns_Month ON BriefingRuns(Month);
            """;
        command.ExecuteNonQuery();
        // Additive migration, serialized across concurrent initializations; existing runs are paid.
        using var transaction = connection.BeginTransaction(deferred: false);
        command.Transaction = transaction;
        command.CommandText = "PRAGMA table_info(BriefingRuns)";
        bool hasBillingMode;
        using (var reader = command.ExecuteReader())
        {
            hasBillingMode = false;
            while (reader.Read())
                if (reader.GetString(1) == "BillingMode") hasBillingMode = true;
        }
        if (!hasBillingMode)
        {
            command.CommandText = "ALTER TABLE BriefingRuns ADD COLUMN BillingMode TEXT NOT NULL DEFAULT 'Paid'";
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public NewsBriefing? GetLatestPublished() => ListPublished(1).FirstOrDefault();
    public bool HasAnyEdition()
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM NewsBriefings)";
        return (long)command.ExecuteScalar()! != 0;
    }
    public NewsBriefing? GetPublished(long id) => Read(id, publishedOnly: true);
    public NewsBriefing? GetForReview(long id) => Read(id, publishedOnly: false);
    public List<NewsBriefing> ListPublished(int limit = 10) => List("published", limit);
    public List<NewsBriefing> ListDrafts(int limit = 20) => List("draft", limit);

    private List<NewsBriefing> List(string status, int limit)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Status,PublishedAtUtc,ReviewedBy,JsonData FROM NewsBriefings WHERE Status=$status ORDER BY PublishedAtUtc DESC,Id DESC LIMIT $limit";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 50));
        using var reader = command.ExecuteReader();
        var result = new List<NewsBriefing>();
        while (reader.Read()) result.Add(FromRow(reader));
        return result;
    }

    private NewsBriefing? Read(long id, bool publishedOnly)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Status,PublishedAtUtc,ReviewedBy,JsonData FROM NewsBriefings WHERE Id=$id AND ($public=0 OR Status='published')";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$public", publishedOnly ? 1 : 0);
        using var reader = command.ExecuteReader();
        return reader.Read() ? FromRow(reader) : null;
    }

    private static NewsBriefing FromRow(SqliteDataReader reader)
    {
        var edition = JsonSerializer.Deserialize<NewsBriefing>(reader.GetString(4), JsonOptions)
            ?? throw new InvalidDataException("Edición inválida en la base.");
        edition.Id = reader.GetInt64(0);
        edition.Status = reader.GetString(1);
        edition.PublishedAtUtc = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        edition.ReviewedBy = reader.IsDBNull(3) ? null : reader.GetString(3);
        return edition;
    }

    public long SaveDraft(NewsBriefing edition)
    {
        Validate(edition);
        edition.Status = "draft";
        edition.PublishedAtUtc = null;
        edition.ReviewedBy = null;
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO NewsBriefings(CreatedAtUtc,Status,JsonData) VALUES($date,'draft',$json); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$date", edition.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(edition, JsonOptions));
        edition.Id = (long)command.ExecuteScalar()!;
        return edition.Id;
    }

    public void Publish(long id, string reviewedBy, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reviewedBy) || reviewedBy.Length > 100)
            throw new ArgumentException("Indique quién revisó la edición (1–100 caracteres).");
        var edition = GetForReview(id) ?? throw new ArgumentException("La edición no existe.");
        Validate(edition);
        if (edition.Status != "draft") throw new InvalidOperationException("Solo se publican borradores.");
        if (edition.CoverageEndUtc > nowUtc || nowUtc - edition.CoverageEndUtc > TimeSpan.FromHours(48))
            throw new InvalidOperationException("El borrador está vencido o tiene una fecha futura; genere una edición nueva.");
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE NewsBriefings SET Status='published',PublishedAtUtc=$date,ReviewedBy=$reviewer WHERE Id=$id AND Status='draft'";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$date", nowUtc.ToString("O"));
        command.Parameters.AddWithValue("$reviewer", reviewedBy.Trim());
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("El borrador ya fue procesado.");
    }

    public void Reject(long id)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE NewsBriefings SET Status='rejected' WHERE Id=$id AND Status='draft'";
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("No existe un borrador pendiente con ese ID.");
    }

    // Atomic slot lock + conservative cost reservation. Unknown/failed requests retain their
    // reservation: a network timeout does not prove that the provider did not charge.
    public bool TryReserve(string slot, string fingerprint, DateTime nowUtc, decimal amount,
        decimal monthlyLimit, string provider, string model, out string reason,
        bool freeTier = false, int maxRequestsPer24Hours = 4, int maxRequestsPerMonth = 124)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.");
        if (freeTier ? amount != 0 || monthlyLimit != 0 || !string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase)
            : amount <= 0 || monthlyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (maxRequestsPer24Hours is < 1 or > 4 || maxRequestsPerMonth is < 1 or > 124)
            throw new ArgumentOutOfRangeException(nameof(maxRequestsPer24Hours));
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM BriefingRuns WHERE Slot=$slot OR (Fingerprint=$hash AND Status IN ('reserved','complete'))";
        command.Parameters.AddWithValue("$slot", slot);
        command.Parameters.AddWithValue("$hash", fingerprint);
        if ((long)command.ExecuteScalar()! > 0)
        {
            reason = "Franja ya procesada o fuentes sin cambios.";
            return false;
        }
        var month = nowUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        // Count all modes/models and statuses, including failed/unknown outcomes. Changing
        // a key or restarting Sync must not refund a request. Rolling 24h avoids timezone resets.
        command.CommandText = "SELECT COUNT(*) FROM BriefingRuns WHERE julianday(StartedAtUtc)>julianday($since)";
        command.Parameters.AddWithValue("$since", nowUtc.AddHours(-24).ToString("O"));
        if ((long)command.ExecuteScalar()! >= maxRequestsPer24Hours)
        {
            reason = "Límite local de solicitudes en 24 horas alcanzado; no se llamó a la API.";
            return false;
        }
        command.CommandText = "SELECT COUNT(*) FROM BriefingRuns WHERE Month=$month";
        command.Parameters.AddWithValue("$month", month);
        if ((long)command.ExecuteScalar()! >= maxRequestsPerMonth)
        {
            reason = "Límite local mensual de solicitudes alcanzado; no se llamó a la API.";
            return false;
        }
        command.CommandText = "SELECT COUNT(*) FROM BriefingRuns WHERE julianday(StartedAtUtc)>julianday($minute)";
        command.Parameters.AddWithValue("$minute", nowUtc.AddMinutes(-1).ToString("O"));
        if ((long)command.ExecuteScalar()! > 0)
        {
            reason = "Ya se reservó una solicitud en el último minuto; no se llamó a la API.";
            return false;
        }
        command.CommandText = "SELECT COALESCE(SUM(COALESCE(ChargedUsd,ReservedUsd)),0) FROM BriefingRuns WHERE Month=$month";
        var spent = Convert.ToDecimal(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!freeTier && spent + amount > monthlyLimit)
        {
            reason = "La reserva excede el presupuesto mensual de síntesis; no se llamó a la API.";
            return false;
        }
        command.CommandText = """
            INSERT INTO BriefingRuns(Slot,Fingerprint,StartedAtUtc,Month,Status,ReservedUsd,Provider,Model,BillingMode)
            VALUES($slot,$hash,$date,$month,'reserved',$amount,$provider,$model,$billing)
            """;
        command.Parameters.AddWithValue("$date", nowUtc.ToString("O"));
        command.Parameters.AddWithValue("$amount", (double)amount);
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$billing", freeTier ? "FreeTier" : "Paid");
        command.ExecuteNonQuery();
        transaction.Commit();
        reason = "Reservado";
        return true;
    }

    public void CompleteRun(string slot, long editionId, int inputTokens, int outputTokens, decimal cost)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BriefingRuns SET Status='complete',EditionId=$id,InputTokens=$input,OutputTokens=$output,ChargedUsd=$cost WHERE Slot=$slot AND Status='reserved'";
        command.Parameters.AddWithValue("$slot", slot);
        command.Parameters.AddWithValue("$id", editionId);
        command.Parameters.AddWithValue("$input", inputTokens);
        command.Parameters.AddWithValue("$output", outputTokens);
        command.Parameters.AddWithValue("$cost", (double)cost);
        command.ExecuteNonQuery();
    }

    public void FailRun(string slot)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BriefingRuns SET Status='failed',Error='Falló generación o validación; reserva retenida.' WHERE Slot=$slot AND Status='reserved'";
        command.Parameters.AddWithValue("$slot", slot);
        command.ExecuteNonQuery();
    }

    public static void Validate(NewsBriefing edition)
    {
        if (edition.CreatedAtUtc.Kind != DateTimeKind.Utc || edition.CoverageStartUtc.Kind != DateTimeKind.Utc
            || edition.CoverageEndUtc.Kind != DateTimeKind.Utc || edition.CoverageStartUtc > edition.CoverageEndUtc
            || edition.CoverageEndUtc > edition.CreatedAtUtc)
            throw new InvalidDataException("Fechas de edición inválidas.");
        if (edition.Sources is not { Count: >= 3 and <= 48 } || edition.Summary is not { Count: >= 1 and <= 4 }
            || edition.Topics is not { Count: >= 4 and <= 6 })
            throw new InvalidDataException("La edición debe contener fuentes, resumen y entre 4 y 6 temas.");
        if (edition.Sources.Any(s => s is null || s.NewsId <= 0 || !SafeUrl(s.Url)
            || string.IsNullOrWhiteSpace(s.SourceName) || string.IsNullOrWhiteSpace(s.PublisherKey)
            || string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.EvidenceText)
            || s.EvidenceText.Length < 100 || s.EvidenceText.Length > 12000
            || s.PublishedAtUtc.Kind != DateTimeKind.Utc || s.PublishedAtUtc > edition.CoverageEndUtc
            || s.PublishedAtUtc < edition.CoverageStartUtc)
            || edition.Sources.Select(s => s.NewsId).Distinct().Count() != edition.Sources.Count)
            throw new InvalidDataException("Fuente sin evidencia, fecha o URL válida.");
        if (edition.Topics.Any(t => t is null || string.IsNullOrWhiteSpace(t.Title) || t.Title.Length > 160)
            || edition.Topics.Select(t => Normalize(t.Title)).Distinct().Count() != edition.Topics.Count
            || edition.Topics.Count(t => t.IsInternational) > 1)
            throw new InvalidDataException("Temas duplicados, inválidos o exceso de cobertura internacional.");
        var sources = edition.Sources.ToDictionary(s => s.NewsId);
        var used = new HashSet<int>();
        foreach (var statement in edition.Summary.Concat(edition.Topics.SelectMany(t => new[] { t.WhatHappened, t.WhyItMatters, t.WhatToWatch })))
        {
            if (statement is null || string.IsNullOrWhiteSpace(statement.Text) || statement.Text.Length > 1800
                || statement.Citations is not { Count: >= 1 and <= 8 })
                throw new InvalidDataException("Cada afirmación necesita texto y evidencia.");
            foreach (var citation in statement.Citations)
            {
                if (citation is null || !sources.TryGetValue(citation.NewsId, out var source)
                    || string.IsNullOrWhiteSpace(citation.Evidence) || citation.Evidence.Length < 20
                    || citation.Evidence.Length > 800 || !Normalize(source.EvidenceText).Contains(Normalize(citation.Evidence), StringComparison.Ordinal))
                    throw new InvalidDataException("Una cita no corresponde a la evidencia suministrada.");
                used.Add(citation.NewsId);
            }
        }
        if (used.Select(id => sources[id].PublisherKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 3)
            throw new InvalidDataException("Se necesitan al menos tres grupos editoriales citados.");
        // This validates provenance, not truth or entailment. Human approval remains mandatory.
    }

    private static string Normalize(string value) => Regex.Replace(value.Normalize(), @"\s+", " ").Trim();
    public static bool SafeUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo)
        && !uri.IsLoopback && uri.HostNameType == UriHostNameType.Dns && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
}
