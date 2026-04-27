using Api.Models;
using Core.Exceptions;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Provides an endpoint for running the repository assistant prompt.
/// </summary>
[ApiController]
[Route("repo-assistant")]
public sealed class RepoAssistantController : ControllerBase
{
    private readonly IRepoAssistantService _repoAssistantService;
    private readonly ILogger<RepoAssistantController> _logger;

    public RepoAssistantController(
        IRepoAssistantService repoAssistantService,
        ILogger<RepoAssistantController> logger
    )
    {
        _repoAssistantService = repoAssistantService;
        _logger = logger;
    }

    /// <summary>
    /// Executes the repo assistant prompt using the configured model endpoint.
    /// </summary>
    /// <param name="request">The user goal and optional repo context.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>The assistant reply when execution succeeds.</returns>
    [HttpPost("run")]
    [ProducesResponseType(typeof(RepoAssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RepoAssistantResponse>> RunAsync(
        [FromBody] RepoAssistantRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var reply = await _repoAssistantService.RunAsync(
                request.UserGoal,
                request.FileContext,
                request.ProjectArea,
                cancellationToken
            );

            return Ok(new RepoAssistantResponse(reply));
        }
        catch (PromptTemplateNotFoundException exception)
        {
            _logger.LogWarning(exception, "Repo assistant prompt template was not found.");
            return Problem(
                title: "Prompt template not found",
                detail: "The repo assistant prompt template is not available on this server.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (UpstreamServiceException exception)
        {
            _logger.LogWarning(
                exception,
                "Repo assistant upstream call failed with status code {StatusCode}.",
                exception.StatusCode
            );

            if (exception.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                return Problem(
                    title: "Assistant service busy",
                    detail: "The repo assistant is temporarily throttled by the configured model endpoint.",
                    statusCode: StatusCodes.Status429TooManyRequests
                );
            }

            return Problem(
                title: "Assistant service unavailable",
                detail: "The repo assistant could not complete the request with the configured model endpoint.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }
    }
}
