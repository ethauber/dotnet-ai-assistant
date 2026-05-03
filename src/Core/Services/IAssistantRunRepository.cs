using Core.Entities;

namespace Core.Services;

/// <summary>
/// Persistence contract for <see cref="AssistantRun"/> and <see cref="AssistantRunEvent"/> entities.
/// Implemented by <c>AssistantRunRepository</c> (EF Core + SQLite).
/// </summary>
public interface IAssistantRunRepository
{
    /// <summary>Persists a new <see cref="AssistantRun"/> and returns it with any store-assigned values.</summary>
    Task<AssistantRun> AddAsync(AssistantRun run, CancellationToken cancellationToken = default);

    /// <summary>Returns the run with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<AssistantRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the <paramref name="count"/> most-recently created runs, ordered descending.</summary>
    Task<IReadOnlyList<AssistantRun>> ListRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default
    );

    /// <summary>Saves all mutations on an already-tracked <see cref="AssistantRun"/>.</summary>
    Task UpdateAsync(AssistantRun run, CancellationToken cancellationToken = default);

    /// <summary>Appends an audit <see cref="AssistantRunEvent"/> to the event log.</summary>
    Task AddEventAsync(AssistantRunEvent runEvent, CancellationToken cancellationToken = default);

    /// <summary>Returns all events for a run ordered chronologically.</summary>
    Task<IReadOnlyList<AssistantRunEvent>> GetEventsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Returns all DB log entries for a run ordered by insertion sequence.</summary>
    Task<IReadOnlyList<RunLog>> GetLogsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default
    );
}
