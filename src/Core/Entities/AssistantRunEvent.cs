namespace Core.Entities;

/// <summary>Identifies whether an event was triggered by the system or a human reviewer.</summary>
public enum ActorType
{
    /// <summary>Action was taken autonomously by the system (e.g. draft generated).</summary>
    System,

    /// <summary>Action was taken by a human (e.g. approve, reject, edit).</summary>
    Human,
}

/// <summary>
/// Immutable audit record for a single state transition or action on an <see cref="AssistantRun"/>.
/// All events are appended; none are deleted or mutated.
/// </summary>
public class AssistantRunEvent
{
    /// <summary>Unique event identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The run this event belongs to.</summary>
    public Guid RunId { get; set; }

    /// <summary>Verb describing what happened (e.g. Created, DraftGenerated, Approved, Rejected).</summary>
    public required string Action { get; set; }

    /// <summary>Whether the action was initiated by the system or a human.</summary>
    public ActorType ActorType { get; set; }

    /// <summary>Optional free-text detail (e.g. rejection reason).</summary>
    public string? Detail { get; set; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}
