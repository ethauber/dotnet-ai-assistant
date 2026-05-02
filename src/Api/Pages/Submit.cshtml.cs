using Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Api.Pages;

public class SubmitModel(IAssistantRunService service) : PageModel
{
    [BindProperty]
    public string UserGoal { get; set; } = string.Empty;

    [BindProperty]
    public string? ProjectArea { get; set; }

    [BindProperty]
    public string? FileContext { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserGoal))
        {
            ErrorMessage = "Goal is required.";
            return Page();
        }

        try
        {
            var run = await service.CreateAsync(
                UserGoal,
                ProjectArea,
                FileContext,
                cancellationToken
            );
            var withDraft = await service.GenerateDraftAsync(run.Id, cancellationToken);
            return RedirectToPage("/Review", new { id = withDraft.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to generate draft: {ex.Message}";
            return Page();
        }
    }
}
