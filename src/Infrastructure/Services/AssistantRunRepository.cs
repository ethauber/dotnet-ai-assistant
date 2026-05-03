using Core.Entities;
using Core.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

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
}
