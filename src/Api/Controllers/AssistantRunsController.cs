using Api.Models;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("assistant-runs")]
public class AssistantRunsController(
    IAssistantRunService service,
    ILogger<AssistantRunsController> logger
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] AssistantRunRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Creating assistant run for goal: {UserGoal}", request.UserGoal);
        var run = await service.CreateAsync(
            request.UserGoal,
            request.ProjectArea,
            request.FileContext,
            request.TemplateName,
            cancellationToken
        );
        logger.LogInformation("AssistantRun {RunId} created", run.Id);
        return CreatedAtAction(
            nameof(GetById),
            new { id = run.Id },
            AssistantRunResponse.From(run)
        );
    }

    [HttpPost("{id:guid}/generate-draft")]
    public async Task<IActionResult> GenerateDraft(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating draft for run {RunId}", id);
        try
        {
            var run = await service.GenerateDraftAsync(id, cancellationToken);
            logger.LogInformation("Draft generated for run {RunId}", id);
            return Ok(AssistantRunResponse.From(run));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Approving run {RunId}", id);
        try
        {
            var run = await service.ApproveAsync(id, cancellationToken);
            return Ok(AssistantRunResponse.From(run));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Rejecting run {RunId}", id);
        try
        {
            var run = await service.RejectAsync(id, request.Reason, cancellationToken);
            return Ok(AssistantRunResponse.From(run));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpPost("{id:guid}/edit-and-approve")]
    public async Task<IActionResult> EditAndApprove(
        Guid id,
        [FromBody] EditAndApproveRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Edit-and-approve for run {RunId}", id);
        try
        {
            var run = await service.EditAndApproveAsync(
                id,
                request.EditedOutput,
                cancellationToken
            );
            return Ok(AssistantRunResponse.From(run));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpPost("{id:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Regenerating draft for run {RunId}", id);
        try
        {
            var run = await service.RegenerateAsync(id, cancellationToken);
            return Ok(AssistantRunResponse.From(run));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var run = await service.GetByIdAsync(id, cancellationToken);
        if (run is null)
            return NotFound();
        return Ok(AssistantRunResponse.From(run));
    }

    [HttpGet]
    public async Task<IActionResult> ListRecent(
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default
    )
    {
        var runs = await service.ListRecentAsync(count, cancellationToken);
        return Ok(runs.Select(AssistantRunResponse.From));
    }

    private ObjectResult NotFoundProblem(string detail) =>
        Problem(title: "Run not found", detail: detail, statusCode: StatusCodes.Status404NotFound);

    private ObjectResult ConflictProblem(string detail) =>
        Problem(
            title: "Invalid state transition",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict
        );
}
