namespace Core.Services;

/// <summary>
/// Runs the repo-assistant prompt template against the configured OpenAI-compatible model endpoint.
/// Implemented by <c>RepoAssistantService</c>, which loads the prompty file, builds dynamic
/// Roslyn AST context from the repo source tree, and calls the model with exponential-backoff
/// retry on HTTP 429.
/// </summary>
public interface IRepoAssistantService
{
    /// <summary>
    /// Executes the prompt with the given goal and optional caller-supplied context.
    /// Repo source context (file tree, Roslyn AST, Core contracts) is injected automatically.
    /// </summary>
    /// <param name="userGoal">The high-level goal or question for the assistant.</param>
    /// <param name="fileContext">Optional additional snippet supplied by the caller; appended after repo context.</param>
    /// <param name="projectArea">Hint for the area of the project (api, core, infra, tests, docs).</param>
    /// <param name="cancellationToken">Propagates cancellation for the HTTP call.</param>
    Task<string> RunAsync(
        string userGoal,
        string? fileContext = null,
        string? projectArea = null,
        CancellationToken cancellationToken = default
    );
}
