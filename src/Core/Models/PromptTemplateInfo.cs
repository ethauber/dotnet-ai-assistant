namespace Core.Models;

/// <summary>
/// Metadata parsed from a .prompty file's YAML front matter.
/// Used by the UI to populate the template dropdown and dynamically render input fields.
/// </summary>
public sealed record PromptTemplateInfo(
    string Name,
    string Description,
    IReadOnlyList<PromptInputInfo> Inputs
);

/// <summary>A single declared input from the prompty <c>inputs:</c> block.</summary>
public sealed record PromptInputInfo(string Key, string Type, string Description, string? Default);
