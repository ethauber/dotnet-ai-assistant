namespace Core.Exceptions;

/// <summary>
/// Thrown by <c>UnconfiguredChatService</c> when Semantic Kernel settings are absent,
/// and may be thrown by other chat adapters when the upstream is unreachable.
/// Maps to HTTP 503 at the API boundary.
/// </summary>
public sealed class ChatServiceUnavailableException(
    string message,
    Exception? innerException = null
) : Exception(message, innerException) { }
