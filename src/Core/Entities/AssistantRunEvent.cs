namespace Core.Entities;

public enum ActorType
{
    System,
    Human,
}

public class AssistantRunEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public required string Action { get; set; }
    public ActorType ActorType { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}
