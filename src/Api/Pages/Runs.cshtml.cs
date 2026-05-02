using Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Api.Pages;

public class RunsModel(IAssistantRunService service) : PageModel
{
    public IReadOnlyList<AssistantRunViewModel> Runs { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var runs = await service.ListRecentAsync(20, cancellationToken);
        Runs = runs.Select(AssistantRunViewModel.From).ToList();
    }
}
