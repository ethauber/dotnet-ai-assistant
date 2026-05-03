using Core.Entities;

namespace Core.Services;

/// <summary>
/// Orchestrates the human-in-the-loop workflow for <see cref="AssistantRun"/> entities.
/// Enforces the state machine: Submitted → DraftGenerated → NeedsHumanReview → Approved / Rejected / Revised.
/// Implemented by <c>AssistantRunService</c>.
/// </summary>
public interface IAssistantRunService
{
    /// <summary>
    /// Creates a new run in <see cref="AssistantRunStatus.Submitted"/> status and records a Created event.
    /// </summary>
    Task<AssistantRun> CreateAsync(
        string userGoal,
        string? projectArea,
        string? fileContext,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Calls the repo assistant to generate a draft and advances the run to
    /// <see cref="AssistantRunStatus.NeedsHumanReview"/>.
    /// Allowed from: <see cref="AssistantRunStatus.Submitted"/>, <see cref="AssistantRunStatus.Rejected"/>.
    /// </summary>
    Task<AssistantRun> GenerateDraftAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the generated draft as-is, setting <c>FinalOutput = GeneratedDraft</c> and
    /// advancing to <see cref="AssistantRunStatus.Approved"/>.
    /// Allowed from: <see cref="AssistantRunStatus.NeedsHumanReview"/>.
    /// </summary>
    Task<AssistantRun> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects the draft and advances to <see cref="AssistantRunStatus.Rejected"/>.
    /// Allowed from: <see cref="AssistantRunStatus.NeedsHumanReview"/>.
    /// </summary>
    Task<AssistantRun> RejectAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Saves the human-edited output as <c>FinalOutput</c> and advances to
    /// <see cref="AssistantRunStatus.Approved"/>.
    /// Allowed from: <see cref="AssistantRunStatus.NeedsHumanReview"/>.
    /// </summary>
    Task<AssistantRun> EditAndApproveAsync(
        Guid id,
        string editedOutput,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resets the run to <see cref="AssistantRunStatus.Submitted"/> and immediately calls
    /// <see cref="GenerateDraftAsync"/> to produce a new draft.
    /// Allowed from: <see cref="AssistantRunStatus.NeedsHumanReview"/>, <see cref="AssistantRunStatus.Rejected"/>.
    /// </summary>
    Task<AssistantRun> RegenerateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the run with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<AssistantRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the <paramref name="count"/> most-recently created runs, ordered descending.</summary>
    Task<IReadOnlyList<AssistantRun>> ListRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default
    );
}
