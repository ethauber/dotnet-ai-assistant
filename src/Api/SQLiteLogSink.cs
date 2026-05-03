using System.Text.Json;
using Microsoft.Data.Sqlite;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Api;

/// <summary>
/// Batched Serilog sink that writes <c>Information</c>+ log events to the <c>Logs</c> table in
/// the application's SQLite database using <c>Microsoft.Data.Sqlite</c> directly — no EF overhead.
/// Wrap with <see cref="PeriodicBatchingSink"/> so events accumulate and flush in bulk rather than
/// one <c>INSERT</c> per log call.
/// </summary>
public sealed class SQLiteLogSink(string sqliteDbPath) : IBatchedLogEventSink
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS Logs (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            Timestamp  TEXT NOT NULL,
            Level      TEXT NOT NULL,
            Message    TEXT NOT NULL,
            Properties TEXT,
            Exception  TEXT
        )
        """;

    private const string InsertSql = """
        INSERT INTO Logs (Timestamp, Level, Message, Properties, Exception)
        VALUES ($ts, $level, $msg, $props, $ex)
        """;

    /// <summary>
    /// Writes a batch of log events to the <c>Logs</c> table in a single transaction.
    /// Creates the table on first call (CREATE TABLE IF NOT EXISTS — idempotent).
    /// Properties are serialized as a flat JSON object enabling <c>json_extract</c> queries.
    /// </summary>
    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        await using var conn = new SqliteConnection($"Data Source={sqliteDbPath}");
        await conn.OpenAsync();

        // Ensure table exists (idempotent on every batch).
        await using var ddl = conn.CreateCommand();
        ddl.CommandText = CreateTableSql;
        await ddl.ExecuteNonQueryAsync();

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = InsertSql;
        cmd.Transaction = tx;

        var tsParam = cmd.Parameters.Add("$ts", SqliteType.Text);
        var levelParam = cmd.Parameters.Add("$level", SqliteType.Text);
        var msgParam = cmd.Parameters.Add("$msg", SqliteType.Text);
        var propsParam = cmd.Parameters.Add("$props", SqliteType.Text);
        var exParam = cmd.Parameters.Add("$ex", SqliteType.Text);

        foreach (var logEvent in batch)
        {
            tsParam.Value = logEvent.Timestamp.UtcDateTime.ToString("o");
            levelParam.Value = logEvent.Level.ToString();
            msgParam.Value = logEvent.RenderMessage();
            propsParam.Value = SerializeProperties(logEvent.Properties);
            exParam.Value = (object?)logEvent.Exception?.ToString() ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    /// <inheritdoc />
    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    /// <summary>
    /// Serializes Serilog log event properties to a flat JSON object.
    /// <see cref="ScalarValue"/> instances are unwrapped to their raw CLR value;
    /// all other value types fall back to their Serilog string representation.
    /// This format allows SQLite <c>json_extract(Properties, '$.Key')</c> queries.
    /// </summary>
    private static string SerializeProperties(
        IReadOnlyDictionary<string, LogEventPropertyValue> props
    )
    {
        var dict = new Dictionary<string, object?>(props.Count);
        foreach (var (key, value) in props)
            dict[key] = value is ScalarValue sv ? sv.Value : value.ToString();
        return JsonSerializer.Serialize(dict);
    }
}
