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
    [Fact]
    public async Task Post_ReturnsReplyFromChatService()
    {
        var mockService = new Mock<IChatService>();
        mockService
            .Setup(s => s.ChatAsync("hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync("world");

        var controller = new ChatController(
            mockService.Object,
            NullLogger<ChatController>.Instance
        );

        var result = await controller.Post(new ChatRequest("hello"), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new ChatResponse("world"));
    }

    [Fact]
    public async Task Post_ReturnsServiceUnavailable_WhenChatIsNotConfigured()
    {
        var mockService = new Mock<IChatService>();
        mockService
            .Setup(s => s.ChatAsync("hello", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChatServiceUnavailableException("Chat service is not configured."));

        var controller = new ChatController(
            mockService.Object,
            NullLogger<ChatController>.Instance
        );

        var result = await controller.Post(new ChatRequest("hello"), CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
    }
}
