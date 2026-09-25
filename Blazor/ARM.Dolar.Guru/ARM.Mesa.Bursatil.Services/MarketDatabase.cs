using Microsoft.Data.Sqlite;

namespace ARM.Mesa.Bursatil.Services;

/// <summary>One connection per operation; both executables share MESA_BURSATIL_DB_PATH.</summary>
public sealed class MarketDatabase
{
    public string FilePath { get; }
    private readonly string connectionString;
    public static readonly string[] SnapshotTables =
        ["CotizacionesDolarJson", "CotizacionesOtrosJson", "FuturoRavaJson", "ProyeccionesDolarJson", "IndicesMercadoJson"];

    public MarketDatabase(string? path = null)
    {
        path = Environment.GetEnvironmentVariable("MESA_BURSATIL_DB_PATH") ?? path;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Configure Database:Path o MESA_BURSATIL_DB_PATH con una ruta absoluta válida para este sistema.", nameof(path));
        FilePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath, Mode = SqliteOpenMode.ReadWriteCreate, DefaultTimeout = 30
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        foreach (var table in SnapshotTables)
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS {table} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FechaEjecucion TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f','now')),
                    JsonData TEXT NOT NULL CHECK(json_valid(JsonData)));
                CREATE INDEX IF NOT EXISTS IX_{table}_Fecha ON {table}(FechaEjecucion DESC, Id DESC);
                """;
            command.ExecuteNonQuery();
        }
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS News (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, Titulo TEXT NOT NULL,
                Resumen TEXT, Url TEXT NOT NULL UNIQUE, Fuente TEXT, FechaPublicacion TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_News_Fecha ON News(FechaPublicacion DESC);
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void SaveSnapshot(string table, string json)
    {
        if (!SnapshotTables.Contains(table)) throw new ArgumentException("Unknown snapshot type", nameof(table));
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {table}(JsonData) SELECT $json
            WHERE COALESCE((SELECT JsonData FROM {table} ORDER BY FechaEjecucion DESC, Id DESC LIMIT 1), '') <> $json;
            """;
        command.Parameters.AddWithValue("$json", json);
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
