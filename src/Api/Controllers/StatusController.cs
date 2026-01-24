using Api.Models;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Provides health status information for the API service.
/// </summary>
[ApiController]
[Route("status")]
public class StatusController : ControllerBase
{
    private readonly IHealthStatusService _healthService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusController"/> class.
    /// </summary>
    /// <param name="healthService">The health status service.</param>
    public StatusController(IHealthStatusService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>
    /// Retrieves the current health status of the API service.
    /// </summary>
    /// <returns>
    /// A <see cref="StatusResponse"/> containing the current status.
    /// Returns "ok" when the service is healthy, or "degraded" when experiencing issues.
    /// </returns>
    /// <response code="200">Returns the current health status.</response>
    [HttpGet]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public ActionResult<StatusResponse> GetStatus()
    {
        var status = _healthService.GetStatus();
        return Ok(new StatusResponse(status));
    }
}
