namespace Core.Entities;

/// <summary>
/// A single structured log entry captured from the application by the database sink.
/// Linked to an <see cref="AssistantRun"/> via <see cref="RunId"/> when the log event
/// carries a <c>{RunId}</c> Serilog property.
/// </summary>
public class RunLog
{
    /// <summary>Auto-increment row identifier. Ordering by Id gives exact chronological order.</summary>
    public long Id { get; set; }

    /// <summary>Run this entry belongs to; null for log entries not tied to a specific run.</summary>
    public Guid? RunId { get; set; }

    /// <summary>HTTP correlation identifier propagated from the <c>X-Correlation-Id</c> header.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the log event.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Serilog severity level string (e.g. Information, Warning, Error, Fatal).</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>Fully rendered log message with all property values substituted.</summary>
    public string RenderedMessage { get; set; } = string.Empty;

    /// <summary>Logger source context, typically the fully-qualified type name of the caller.</summary>
    public string? SourceContext { get; set; }

    /// <summary>Full exception string when an exception was attached; null otherwise.</summary>
    public string? Exception { get; set; }
}
