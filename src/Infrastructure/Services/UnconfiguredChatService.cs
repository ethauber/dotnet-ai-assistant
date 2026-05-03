using Core.Exceptions;
using Core.Services;

namespace Infrastructure.Services;

/// <summary>
/// Fallback <see cref="IChatService"/> registered when Semantic Kernel is not configured.
/// Every call throws <see cref="ChatServiceUnavailableException"/>, which the controller
/// maps to HTTP 503.
/// </summary>
public sealed class UnconfiguredChatService : IChatService
{
    public Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        throw new ChatServiceUnavailableException(
            "The chat service is not configured with Semantic Kernel settings."
        );
    }
}
