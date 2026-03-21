using Core.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Infrastructure.Services;

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
