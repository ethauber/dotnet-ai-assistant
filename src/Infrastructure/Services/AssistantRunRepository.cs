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
    ) =>
        await db
            .AssistantRuns.OrderByDescending(r => r.CreatedUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

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
}
