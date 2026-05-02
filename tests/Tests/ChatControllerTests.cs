using Api.Controllers;
using Api.Models;
using Core.Exceptions;
using Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests;

public class ChatControllerTests
{
    // xUnit constructor = beforeEach: shared mock and controller wired once per test
    private readonly Mock<IChatService> _chatService = new();
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _controller = new ChatController(_chatService.Object, NullLogger<ChatController>.Instance);
    }

    [Fact]
    public async Task Post_ReturnsReplyFromChatService()
    {
        _chatService
            .Setup(s => s.ChatAsync("hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync("world");

        var result = await _controller.Post(new ChatRequest("hello"), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new ChatResponse("world"));
    }

    [Fact]
    public async Task Post_ReturnsServiceUnavailable_WhenChatIsNotConfigured()
    {
        _chatService
            .Setup(s => s.ChatAsync("hello", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChatServiceUnavailableException("Chat service is not configured."));

        var result = await _controller.Post(new ChatRequest("hello"), CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
    }
}
