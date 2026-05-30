using Core.Entities;
using Core.Services;
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// EF Core + SQLite implementation of <see cref="IAssistantRunRepository"/>.
/// Note: SQLite does not support <see cref="DateTimeOffset"/> in ORDER BY; affected queries
/// fetch all rows and sort client-side.
/// </summary>
public class AssistantRunRepository(AssistantDbContext db) : IAssistantRunRepository
{
    public async Task<AssistantRun> AddAsync(
        AssistantRun run,
        CancellationToken cancellationToken = default
    )
    {
        db.AssistantRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<AssistantRun> AddWithEventAsync(
        AssistantRun run,
        AssistantRunEvent runEvent,
        CancellationToken cancellationToken = default
    )
    {
        db.AssistantRuns.Add(run);
        db.AssistantRunEvents.Add(runEvent);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public Task<AssistantRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => db.AssistantRuns.FindAsync([id], cancellationToken).AsTask();

    public async Task<IReadOnlyList<AssistantRun>> ListRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default
    )
    {
        // SQLite does not support DateTimeOffset in ORDER BY; sort on the client after fetching.
        var all = await db.AssistantRuns.ToListAsync(cancellationToken);
        return all.OrderByDescending(r => r.CreatedUtc).Take(count).ToList();
    }

    public async Task UpdateAsync(AssistantRun run, CancellationToken cancellationToken = default)
    {
        db.AssistantRuns.Update(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithEventAsync(
        AssistantRun run,
        AssistantRunEvent runEvent,
        CancellationToken cancellationToken = default
    )
    {
        db.AssistantRuns.Update(run);
        db.AssistantRunEvents.Add(runEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddEventAsync(
        AssistantRunEvent runEvent,
        CancellationToken cancellationToken = default
    )
    {
        db.AssistantRunEvents.Add(runEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantRunEvent>> GetEventsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default
    )
    {
        // SQLite does not support DateTimeOffset in ORDER BY; sort on the client after fetching.
        var events = await db
            .AssistantRunEvents.Where(e => e.RunId == runId)
            .ToListAsync(cancellationToken);
        return events.OrderBy(e => e.OccurredUtc).ToList();
    }

    /// <summary>
    /// Returns all structured log entries captured for <paramref name="runId"/>, ordered by
    /// insertion sequence (<c>Id</c> ascending).
    /// </summary>
    /// <remarks>
    /// Queries Serilog's <c>Logs</c> table written by <c>SQLiteLogSink</c> via raw ADO.NET —
    /// this table is not EF-managed. Uses <c>json_extract(Properties, '$.RunId')</c> to match
    /// entries. Returns an empty list if the table does not yet exist (e.g. first startup or
    /// test environments where no logs have been flushed).
    /// </remarks>
    public async Task<IReadOnlyList<RunLog>> GetLogsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default
    )
    {
        // SQLiteLogSink writes to the "Logs" table (not EF-managed).
        // Query via raw ADO.NET to stay outside EF's change tracker.
        var connectionString = db.Database.GetConnectionString()!;
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id,
                   Timestamp,
                   Level,
                   Message,
                   json_extract(Properties, '$.SourceContext') AS SourceContext,
                   Exception,
                   json_extract(Properties, '$.RunId')          AS RunId,
                   COALESCE(json_extract(Properties, '$.CorrelationId'), '') AS CorrelationId
            FROM   Logs
            WHERE  json_extract(Properties, '$.RunId') = $RunId
            ORDER  BY Id
            """;
        cmd.Parameters.AddWithValue("$RunId", runId.ToString());

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var results = new List<RunLog>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(
                    new RunLog
                    {
                        Id = reader.GetInt64(0),
                        Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
                        Level = reader.GetString(2),
                        RenderedMessage = reader.GetString(3),
                        SourceContext = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Exception = reader.IsDBNull(5) ? null : reader.GetString(5),
                        RunId =
                            reader.IsDBNull(6) ? null
                            : Guid.TryParse(reader.GetString(6), out var g) ? g
                            : null,
                        CorrelationId = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    }
                );
            }
            return results;
        }
        catch (SqliteException)
        {
            // Logs table may not exist yet (first startup, test environment).
            return [];
        }
    }
}
