using Core.Entities;

namespace Core.Services;

public interface IAssistantRunService
{
    Task<AssistantRun> CreateAsync(
        string userGoal,
        string? projectArea,
        string? fileContext,
        CancellationToken cancellationToken = default
    );

    Task<AssistantRun> GenerateDraftAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AssistantRun> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AssistantRun> RejectAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default
    );

    Task<AssistantRun> EditAndApproveAsync(
        Guid id,
        string editedOutput,
        CancellationToken cancellationToken = default
    );

    Task<AssistantRun> RegenerateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AssistantRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssistantRun>> ListRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default
    );
}
