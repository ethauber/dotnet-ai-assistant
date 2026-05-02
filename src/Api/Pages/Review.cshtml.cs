using Core.Entities;
using Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Api.Pages;

public class ReviewModel(IAssistantRunService service) : PageModel
{
    public AssistantRunViewModel? Run { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await service.GetByIdAsync(id, cancellationToken);
        if (run is null)
            return NotFound();
        Run = AssistantRunViewModel.From(run);
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(id, r => service.ApproveAsync(r, cancellationToken));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken cancellationToken)
    {
        await ExecuteAsync(id, r => service.RejectAsync(r, cancellationToken: cancellationToken));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRegenerateAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(id, r => service.RegenerateAsync(r, cancellationToken));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditAndApproveAsync(
        Guid id,
        string editedOutput,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
            id,
            r => service.EditAndApproveAsync(r, editedOutput, cancellationToken)
        );
        return RedirectToPage(new { id });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ExecuteAsync(Guid id, Func<Guid, Task<AssistantRun>> action)
    {
        try
        {
            var result = await action(id);
            Run = AssistantRunViewModel.From(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            var run = await service.GetByIdAsync(id);
            Run = run is not null ? AssistantRunViewModel.From(run) : null;
        }
    }
}

public record AssistantRunViewModel(
    Guid Id,
    string UserGoal,
    string? ProjectArea,
    string? GeneratedDraft,
    string? FinalOutput,
    string Status,
    DateTimeOffset CreatedUtc
)
{
    public static AssistantRunViewModel From(AssistantRun r) =>
        new(
            r.Id,
            r.UserGoal,
            r.ProjectArea,
            r.GeneratedDraft,
            r.FinalOutput,
            r.Status.ToString(),
            r.CreatedUtc
        );
}
