using Api.Models;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>Sends a message and returns the assistant reply.</summary>
    /// <response code="200">Returns the assistant reply.</response>
    /// <response code="400">Request body is invalid.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken
    )
    {
        var reply = await _chatService.ChatAsync(request.Message, cancellationToken);
        return Ok(new ChatResponse(reply));
    }
}
