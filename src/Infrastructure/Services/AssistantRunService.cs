using Core.Entities;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Orchestrates the human-in-the-loop workflow. Delegates persistence to
/// <see cref="IAssistantRunRepository"/> and AI generation to <see cref="IRepoAssistantService"/>.
/// All state-machine guards throw <see cref="InvalidOperationException"/> on invalid transitions.
/// </summary>
public class AssistantRunService(
    IAssistantRunRepository repository,
    IRepoAssistantService repoAssistant,
    ILogger<AssistantRunService> logger
) : IAssistantRunService
{
    // High-performance LoggerMessage delegates — avoids per-call boxing and string allocation.
    private static readonly Action<ILogger, Guid, string?, Exception?> LogRunCreated =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Information,
            new EventId(1, "RunCreated"),
            "Run {RunId} created template={TemplateName}"
        );

    private static readonly Action<ILogger, Guid, string?, Exception?> LogGeneratingDraft =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Information,
            new EventId(2, "GeneratingDraft"),
            "Generating draft for run {RunId} template={TemplateName}"
        );

    private static readonly Action<ILogger, Guid, string?, Exception?> LogDraftGenerated =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Information,
            new EventId(3, "DraftGenerated"),
            "Draft generated for run {RunId} version={Version}"
        );

    private static readonly Action<ILogger, Guid, Exception?> LogRunApproved =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, "RunApproved"),
            "Run {RunId} approved"
        );

    private static readonly Action<ILogger, Guid, string, Exception?> LogRunRejected =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(5, "RunRejected"),
            "Run {RunId} rejected reason={Reason}"
        );

    private static readonly Action<ILogger, Guid, Exception?> LogRunEditedAndApproved =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(6, "RunEditedAndApproved"),
            "Run {RunId} edited and approved"
        );

    private static readonly Action<ILogger, Guid, Exception?> LogRegenerationRequested =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(7, "RegenerationRequested"),
            "Run {RunId} regeneration requested"
        );

    public async Task<AssistantRun> CreateAsync(
        string userGoal,
        string? projectArea,
        string? fileContext,
        string? promptTemplateName = null,
        CancellationToken cancellationToken = default
    )
    {
        var run = new AssistantRun
        {
            UserGoal = userGoal,
            ProjectArea = projectArea,
            FileContext = fileContext,
            PromptTemplateName = promptTemplateName ?? "demo-assistant",
        };

        await repository.AddAsync(run, cancellationToken);
        await repository.AddEventAsync(
            new AssistantRunEvent
            {
                RunId = run.Id,
                Action = "Created",
                ActorType = ActorType.Human,
                Detail = run.PromptTemplateName,
            },
            cancellationToken
        );

        LogRunCreated(logger, run.Id, run.PromptTemplateName, null);
        return run;
    }

    public async Task<AssistantRun> GenerateDraftAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var run = await RequireRunAsync(id, cancellationToken);
        RequireStatus(run, AssistantRunStatus.Submitted, AssistantRunStatus.Rejected);

        LogGeneratingDraft(logger, id, run.PromptTemplateName, null);

        var result = await repoAssistant.RunAsync(
            run.PromptTemplateName ?? "demo-assistant",
            run.UserGoal,
            run.FileContext,
            run.ProjectArea,
            cancellationToken
        );
        run.GeneratedDraft = result.Reply;
        run.PromptTemplateVersion = result.TemplateVersion;
        run.Status = AssistantRunStatus.NeedsHumanReview;

        await CommitAsync(
            run,
            "DraftGenerated",
            ActorType.System,
            detail: $"template:{run.PromptTemplateName} v{run.PromptTemplateVersion}\n\n{run.GeneratedDraft}",
            cancellationToken: cancellationToken
        );
        LogDraftGenerated(logger, id, run.PromptTemplateVersion, null);
        return run;
    }

    public async Task<AssistantRun> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var run = await RequireRunAsync(id, cancellationToken);
        RequireStatus(run, AssistantRunStatus.NeedsHumanReview);

        run.FinalOutput = run.GeneratedDraft;
        run.Status = AssistantRunStatus.Approved;

        await CommitAsync(
            run,
            "Approved",
            ActorType.Human,
            detail: run.FinalOutput,
            cancellationToken: cancellationToken
        );
        LogRunApproved(logger, id, null);
        return run;
    }

    public async Task<AssistantRun> RejectAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default
    )
    {
        var run = await RequireRunAsync(id, cancellationToken);
        RequireStatus(run, AssistantRunStatus.NeedsHumanReview);

        run.Status = AssistantRunStatus.Rejected;

        await CommitAsync(run, "Rejected", ActorType.Human, reason, cancellationToken);
        LogRunRejected(logger, id, reason ?? string.Empty, null);
        return run;
    }

    public async Task<AssistantRun> EditAndApproveAsync(
        Guid id,
        string editedOutput,
        CancellationToken cancellationToken = default
    )
    {
        var run = await RequireRunAsync(id, cancellationToken);
        RequireStatus(run, AssistantRunStatus.NeedsHumanReview);

        run.FinalOutput = editedOutput;
        run.Status = AssistantRunStatus.Approved;

        await CommitAsync(
            run,
            "EditedAndApproved",
            ActorType.Human,
            detail: $"original:\n{run.GeneratedDraft}\n\n---edited & approved:\n{editedOutput}",
            cancellationToken: cancellationToken
        );
        LogRunEditedAndApproved(logger, id, null);
        return run;
    }

    public async Task<AssistantRun> RegenerateAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var run = await RequireRunAsync(id, cancellationToken);
        RequireStatus(run, AssistantRunStatus.NeedsHumanReview, AssistantRunStatus.Rejected);

        run.Status = AssistantRunStatus.Submitted;
        run.GeneratedDraft = null;

        await CommitAsync(
            run,
            "RegenerateRequested",
            ActorType.Human,
            detail: run.PromptTemplateName,
            cancellationToken: cancellationToken
        );
        LogRegenerationRequested(logger, id, null);
        return await GenerateDraftAsync(run.Id, cancellationToken);
    }

    public Task<AssistantRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<AssistantRun>> ListRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default
    ) => repository.ListRecentAsync(count, cancellationToken);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<AssistantRun> RequireRunAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await repository.GetByIdAsync(id, cancellationToken);
        if (run is null)
            throw new KeyNotFoundException($"AssistantRun {id} not found.");
        return run;
    }

    private static void RequireStatus(AssistantRun run, params AssistantRunStatus[] allowed)
    {
        if (!allowed.Contains(run.Status))
            throw new InvalidOperationException(
                $"Cannot perform action on run with status '{run.Status}'. Expected one of: {string.Join(", ", allowed)}."
            );
    }

    private async Task CommitAsync(
        AssistantRun run,
        string action,
        ActorType actorType,
        string? detail = null,
        CancellationToken cancellationToken = default
    )
    {
        run.UpdatedUtc = DateTimeOffset.UtcNow;
        await repository.UpdateAsync(run, cancellationToken);
        await repository.AddEventAsync(
            new AssistantRunEvent
            {
                RunId = run.Id,
                Action = action,
                ActorType = actorType,
                Detail = detail,
            },
            cancellationToken
        );
    }
}
