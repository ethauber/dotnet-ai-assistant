using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Api.Pages;

public class ErrorModel : PageModel
{
    public new int StatusCode { get; private set; }
    public string Title { get; private set; } = "Something went wrong";
    public string Description { get; private set; } = "An unexpected error occurred.";

    public void OnGet(int? statusCode)
    {
        StatusCode = statusCode ?? 500;
        (Title, Description) = StatusCode switch
        {
            404 => (
                "Page Not Found",
                "The page or resource you were looking for doesn't exist or has been moved."
            ),
            409 => ("Conflict", "The action you attempted isn't allowed in the current state."),
            500 => ("Server Error", "Something went wrong on our end. Check the logs for details."),
            _ => ("Error", $"An error occurred (HTTP {StatusCode})."),
        };
    }
}
