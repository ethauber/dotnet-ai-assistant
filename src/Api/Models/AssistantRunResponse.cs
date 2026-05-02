using Core.Entities;

namespace Api.Models;

public record AssistantRunResponse(
    Guid Id,
    string UserGoal,
    string? ProjectArea,
    string? FileContext,
    string? GeneratedDraft,
    string? FinalOutput,
    string Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc
)
{
    public static AssistantRunResponse From(AssistantRun run) =>
        new(
            run.Id,
            run.UserGoal,
            run.ProjectArea,
            run.FileContext,
            run.GeneratedDraft,
            run.FinalOutput,
            run.Status.ToString(),
            run.CreatedUtc,
            run.UpdatedUtc
        );
}
