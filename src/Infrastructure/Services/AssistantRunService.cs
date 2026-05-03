using Core.Entities;
using Core.Services;

namespace Infrastructure.Services;

/// <summary>
/// Orchestrates the human-in-the-loop workflow. Delegates persistence to
/// <see cref="IAssistantRunRepository"/> and AI generation to <see cref="IRepoAssistantService"/>.
/// All state-machine guards throw <see cref="InvalidOperationException"/> on invalid transitions.
/// </summary>
public class AssistantRunService(
    IAssistantRunRepository repository,
    IRepoAssistantService repoAssistant
) : IAssistantRunService
{
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
            },
            cancellationToken
        );

        return run;
    }

    public async Task<AssistantRun> GenerateDraftAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var run = await RequireRunAsync(id, cancellationToken);
        RequireStatus(run, AssistantRunStatus.Submitted, AssistantRunStatus.Rejected);

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
            cancellationToken: cancellationToken
        );
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

        await CommitAsync(run, "Approved", ActorType.Human, cancellationToken: cancellationToken);
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
            cancellationToken: cancellationToken
        );
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
            cancellationToken: cancellationToken
        );
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
