using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Api.Pages;

public class SubmitModel(
    IAssistantRunService service,
    IPromptTemplateService templateService,
    ILogger<SubmitModel> logger
) : PageModel
{
    [BindProperty]
    public string UserGoal { get; set; } = string.Empty;

    [BindProperty]
    public string? ProjectArea { get; set; }

    [BindProperty]
    public string? FileContext { get; set; }

    [BindProperty]
    public string TemplateName { get; set; } = "demo-assistant";

    public IReadOnlyList<PromptTemplateInfo> Templates { get; private set; } = [];

    public SelectList TemplateSelectList { get; private set; } =
        new SelectList(Enumerable.Empty<object>());

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Templates = await templateService.ListAsync(cancellationToken);
        TemplateSelectList = BuildSelectList();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Templates = await templateService.ListAsync(cancellationToken);
        TemplateSelectList = BuildSelectList();

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
                TemplateName,
                cancellationToken
            );
            var withDraft = await service.GenerateDraftAsync(run.Id, cancellationToken);
            return RedirectToPage("/Review", new { id = withDraft.Id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate draft for user goal");
            ErrorMessage = "Failed to generate draft. Please try again later.";
            return Page();
        }
    }

    private SelectList BuildSelectList() =>
        new(
            Templates.Select(t => new
            {
                Value = t.Name,
                Text = string.IsNullOrWhiteSpace(t.Description)
                    ? t.Name
                    : $"{t.Name} \u2014 {t.Description}",
            }),
            "Value",
            "Text",
            TemplateName
        );
}
