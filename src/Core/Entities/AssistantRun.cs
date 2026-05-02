namespace Core.Entities;

public class AssistantRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserGoal { get; set; }
    public string? ProjectArea { get; set; }
    public string? FileContext { get; set; }
    public string? GeneratedDraft { get; set; }
    public string? FinalOutput { get; set; }
    public AssistantRunStatus Status { get; set; } = AssistantRunStatus.Submitted;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
