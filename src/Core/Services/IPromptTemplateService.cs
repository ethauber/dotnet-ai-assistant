using Core.Models;

namespace Core.Services;

/// <summary>
/// Discovers and describes prompt templates available in the configured prompts directory.
/// Used by the UI to populate template dropdowns and render dynamic input fields.
/// Implemented by <c>PromptTemplateService</c>.
/// </summary>
public interface IPromptTemplateService
{
    /// <summary>
    /// Returns metadata for every <c>.prompty</c> file in the prompts directory,
    /// ordered by name. Returns an empty list if the directory does not exist.
    /// </summary>
    Task<IReadOnlyList<PromptTemplateInfo>> ListAsync(
        CancellationToken cancellationToken = default
    );
}
