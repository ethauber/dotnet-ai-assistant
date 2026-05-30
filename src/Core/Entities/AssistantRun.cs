namespace Core.Entities;

/// <summary>
/// Aggregate root for a single human-in-the-loop assistant workflow run.
/// Progresses through a state machine enforced by <c>AssistantRunService</c>:
/// Submitted → DraftGenerated → NeedsHumanReview → Approved | Rejected | Revised.
/// </summary>
public class AssistantRun
{
    /// <summary>Unique run identifier. Assigned on construction.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The high-level goal the user submitted.</summary>
    public required string UserGoal { get; set; }

    /// <summary>Optional hint for the area of the project (api, core, infra, tests, docs).</summary>
    public string? ProjectArea { get; set; }

    /// <summary>Optional file snippet provided by the user alongside the goal.</summary>
    public string? FileContext { get; set; }

    /// <summary>AI-generated draft, populated by <c>GenerateDraftAsync</c>. Null until generated.</summary>
    public string? GeneratedDraft { get; set; }

    /// <summary>Human-approved output. Set on Approve or EditAndApprove. Null until approved.</summary>
    public string? FinalOutput { get; set; }

    /// <summary>Current workflow status.</summary>
    public AssistantRunStatus Status { get; set; } = AssistantRunStatus.Submitted;

    /// <summary>UTC timestamp when the run was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the most recent state change.</summary>
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Name of the prompty template used to generate the draft (e.g. <c>"demo-assistant"</c>).
    /// Null for runs created before template selection was introduced.
    /// </summary>
    public string? PromptTemplateName { get; set; }

    /// <summary>
    /// First 8 hex characters of the SHA-256 hash of the prompty file content at the time of
    /// draft generation. Null until <c>GenerateDraftAsync</c> completes.
    /// </summary>
    public string? PromptTemplateVersion { get; set; }
}
