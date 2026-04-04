using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public sealed class RepoAssistantRequest
{
    [Required]
    public string UserGoal { get; set; } = string.Empty;

    public string? FileContext { get; set; }

    public string? ProjectArea { get; set; }
}
