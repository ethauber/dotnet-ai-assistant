namespace Core.Services;

public interface IRepoAssistantService
{
    Task<string> RunAsync(
        string userGoal,
        string? fileContext = null,
        string? projectArea = null,
        CancellationToken cancellationToken = default
    );
}
