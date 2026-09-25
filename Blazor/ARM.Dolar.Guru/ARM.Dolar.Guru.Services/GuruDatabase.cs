using Microsoft.Data.Sqlite;

namespace ARM.Dolar.Guru.Services;

/// <summary>One connection per operation; both executables share DOLAR_GURU_DB_PATH.</summary>
public sealed class GuruDatabase
{
    public string FilePath { get; }
    private readonly string connectionString;
    public static readonly string[] SnapshotTables =
        ["CotizacionesDolarJson", "CotizacionesOtrosJson", "FuturoRavaJson", "ProyeccionesDolarJson"];

    public GuruDatabase(string? path = null)
    {
        path = Environment.GetEnvironmentVariable("DOLAR_GURU_DB_PATH") ?? path;
        FilePath = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DolarGuru", "market.db"));
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
