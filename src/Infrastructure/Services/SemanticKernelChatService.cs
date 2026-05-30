using Core.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Infrastructure.Services;

/// <summary>
/// Single-turn chat implementation backed by Semantic Kernel's <see cref="IChatCompletionService"/>.
/// Supports any OpenAI-compatible endpoint (Ollama, Azure OpenAI, OpenAI). Registered in DI
/// when <c>SemanticKernel:ModelId</c> and <c>SemanticKernel:ApiKey</c> are configured.
/// </summary>
public class SemanticKernelChatService : IChatService
{
    private readonly IChatCompletionService _chatCompletion;

    public SemanticKernelChatService(IChatCompletionService chatCompletion)
    {
        _chatCompletion = chatCompletion;
    }

    public async Task<string> ChatAsync(
        string userMessage,
        CancellationToken cancellationToken = default
    )
    {
        var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        history.AddUserMessage(userMessage);

        var result = await _chatCompletion.GetChatMessageContentAsync(
            history,
            cancellationToken: cancellationToken
        );

        return result.Content ?? string.Empty;
    }
}
