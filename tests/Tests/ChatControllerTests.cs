using Api.Controllers;
using Api.Models;
using Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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

        var controller = new ChatController(mockService.Object);

        var result = await controller.Post(new ChatRequest("hello"), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new ChatResponse("world"));
    }
}
