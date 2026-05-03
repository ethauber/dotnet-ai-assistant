namespace Core.Entities;

/// <summary>
/// Workflow state machine for an <see cref="AssistantRun"/>.
/// Valid transitions are enforced by <c>AssistantRunService</c>.
/// </summary>
public enum AssistantRunStatus
{
    /// <summary>Goal submitted by the user; draft not yet generated.</summary>
    Submitted,

    /// <summary>Internal transitional state set during draft generation (reserved for future async use).</summary>
    DraftGenerated,

    /// <summary>Draft generated; awaiting human review (approve / reject / edit-and-approve).</summary>
    NeedsHumanReview,

    /// <summary>Human approved the draft. <c>FinalOutput</c> is set. Terminal state.</summary>
    Approved,

    /// <summary>Human rejected the draft. Run can be regenerated.</summary>
    Rejected,

    /// <summary>Human edited and approved. <c>FinalOutput</c> contains the edited text. Terminal state.</summary>
    Revised,
}
