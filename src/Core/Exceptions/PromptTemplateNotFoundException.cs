namespace Core.Exceptions;

/// <summary>
/// Thrown by <c>RepoAssistantService</c> when the <c>.prompty</c> file cannot be located
/// at the expected path. Maps to HTTP 404 at the API boundary.
/// </summary>
public sealed class PromptTemplateNotFoundException(string path)
    : Exception($"Prompt template was not found at '{path}'.")
{
    /// <summary>The filesystem path that was searched.</summary>
    public string Path { get; } = path;
}
