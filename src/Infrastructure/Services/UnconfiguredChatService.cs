using Core.Exceptions;
using Core.Services;

namespace Infrastructure.Services;

public sealed class UnconfiguredChatService : IChatService
{
    public Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        throw new ChatServiceUnavailableException(
            "The chat service is not configured with Semantic Kernel settings."
        );
    }
}
