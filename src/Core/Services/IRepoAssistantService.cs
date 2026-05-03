namespace Core.Services;

/// <summary>
/// The reply and version identifier returned by <see cref="IRepoAssistantService.RunAsync"/>.
/// </summary>
/// <param name="Reply">The assistant's text reply.</param>
/// <param name="TemplateVersion">
/// First 8 hex characters of the SHA-256 hash of the prompty file content at the time of the call.
/// Used to record which exact revision of the template produced a given draft.
/// </param>
public sealed record PromptRunResult(string Reply, string TemplateVersion);

/// <summary>
/// Runs a named prompt template against the configured OpenAI-compatible model endpoint.
/// Implemented by <c>RepoAssistantService</c>, which loads the prompty file, builds dynamic
/// Roslyn AST context from the repo source tree, and calls the model with exponential-backoff
/// retry on HTTP 429.
/// </summary>
public interface IRepoAssistantService
{
    /// <summary>
    /// Executes the named template with the given goal and optional caller-supplied context.
    /// Repo source context (file tree, Roslyn AST, Core contracts) is injected automatically.
    /// </summary>
    /// <param name="templateName">
    /// Name of the <c>.prompty</c> file to use (without extension), e.g. <c>"demo-assistant"</c>.
    /// </param>
    /// <param name="userGoal">The high-level goal or question for the assistant.</param>
    /// <param name="fileContext">Optional additional snippet supplied by the caller; appended after repo context.</param>
    /// <param name="projectArea">Hint for the area of the project (api, core, infra, tests, docs).</param>
    /// <param name="cancellationToken">Propagates cancellation for the HTTP call.</param>
    Task<PromptRunResult> RunAsync(
        string templateName,
        string userGoal,
        string? fileContext = null,
        string? projectArea = null,
        CancellationToken cancellationToken = default
    );
}
