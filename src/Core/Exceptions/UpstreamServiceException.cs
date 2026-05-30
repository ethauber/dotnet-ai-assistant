namespace Core.Exceptions;

/// <summary>
/// Thrown by <c>RepoAssistantService</c> when the model endpoint returns a non-2xx response.
/// Maps to HTTP 429 (when <see cref="StatusCode"/> is 429) or HTTP 503 otherwise.
/// </summary>
public sealed class UpstreamServiceException : Exception
{
    public UpstreamServiceException(
        string message,
        int? statusCode = null,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status code returned by the upstream, if available.</summary>
    public int? StatusCode { get; }
}
