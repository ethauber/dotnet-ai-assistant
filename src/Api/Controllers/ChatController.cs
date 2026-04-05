using Api.Models;
using Core.Exceptions;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>Sends a message and returns the assistant reply.</summary>
    /// <response code="200">Returns the assistant reply.</response>
    /// <response code="400">Request body is invalid.</response>
    /// <response code="503">Chat service is not configured or temporarily unavailable.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var reply = await _chatService.ChatAsync(request.Message, cancellationToken);
            return Ok(new ChatResponse(reply));
        }
        catch (ChatServiceUnavailableException exception)
        {
            _logger.LogWarning(exception, "Chat service is unavailable.");
            return Problem(
                title: "Chat service unavailable",
                detail: "The chat service is not configured or is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }
    }
}
