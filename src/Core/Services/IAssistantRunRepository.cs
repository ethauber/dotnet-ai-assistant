using Core.Entities;

namespace Core.Services;

public interface IAssistantRunRepository
{
    Task<AssistantRun> AddAsync(AssistantRun run, CancellationToken cancellationToken = default);
    Task<AssistantRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssistantRun>> ListRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default
    );
    Task UpdateAsync(AssistantRun run, CancellationToken cancellationToken = default);
    Task AddEventAsync(AssistantRunEvent runEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssistantRunEvent>> GetEventsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default
    );
}
