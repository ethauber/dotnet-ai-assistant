using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public sealed class RepoAssistantRequest
{
    [Required]
    public string UserGoal { get; set; } = string.Empty;

    public string? FileContext { get; set; }

    public string? ProjectArea { get; set; }

    /// <summary>Name of the prompty template to use. Defaults to <c>"repo-assistant"</c>.</summary>
    public string? TemplateName { get; set; }
}
