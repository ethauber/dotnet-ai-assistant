namespace Core.Services;

/// <summary>
/// Single-turn chat service abstraction.
/// Implemented by <c>SemanticKernelChatService</c> (Ollama / OpenAI-compatible)
/// and <c>UnconfiguredChatService</c> (throws when Semantic Kernel is not configured).
/// </summary>
public interface IChatService
{
    /// <summary>Sends <paramref name="userMessage"/> to the configured model and returns the reply.</summary>
    Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default);
}
