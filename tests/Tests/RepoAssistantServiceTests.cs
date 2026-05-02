using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Exceptions;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public sealed class RepoAssistantServiceTests
{
    [Fact]
    public async Task RunAsync_Should_Load_Prompt_And_Return_Assistant_Content()
    {
        var promptPath = GetPromptPath();
        string? requestPayload = null;
        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestPayload = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"choices\":[{\"message\":{\"content\":\"assistant reply\"}}]}",
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
        );
        var service = CreateService(handler, promptPath);

        var result = await service.RunAsync(
            "Summarize the repo",
            fileContext: "StatusController.cs",
            projectArea: "api"
        );

        result.Should().Be("assistant reply");
        requestPayload.Should().NotBeNull();

        using var requestDocument = JsonDocument.Parse(requestPayload!);
        var messages = requestDocument.RootElement.GetProperty("messages");
        var systemMessage = messages[0].GetProperty("content").GetString();

        systemMessage.Should().Contain("Summarize the repo");
        systemMessage.Should().Contain("StatusController.cs");
        systemMessage.Should().Contain("api");
        systemMessage.Should().NotContain("{user_goal}");
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Prompt_File_Is_Missing()
    {
        var service = CreateService(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
            "missing.prompty"
        );

        var act = () => service.RunAsync("Summarize the repo");

        await act.Should().ThrowAsync<PromptTemplateNotFoundException>();
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Upstream_Returns_NonSuccess_Status()
    {
        var service = CreateService(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)),
            GetPromptPath()
        );

        var act = () => service.RunAsync("Summarize the repo");

        var exception = await act.Should().ThrowAsync<UpstreamServiceException>();
        exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Upstream_Returns_Invalid_Json()
    {
        var service = CreateService(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("not json", Encoding.UTF8, "application/json"),
                    }
                ),
            GetPromptPath()
        );

        var act = () => service.RunAsync("Summarize the repo");

        await act.Should()
            .ThrowAsync<UpstreamServiceException>()
            .Where(exception => exception.InnerException is JsonException);
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Upstream_Returns_No_Assistant_Content()
    {
        var service = CreateService(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"choices\":[]}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                ),
            GetPromptPath()
        );

        var act = () => service.RunAsync("Summarize the repo");

        await act.Should()
            .ThrowAsync<UpstreamServiceException>()
            .WithMessage("*no assistant content*");
    }

    [Fact]
    public async Task RunAsync_Should_Retry_When_Upstream_Is_Throttled()
    {
        var attemptCount = 0;
        var service = CreateService(
            (_, _) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    var throttledResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(
                        TimeSpan.Zero
                    );
                    return Task.FromResult(throttledResponse);
                }
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"choices\":[{\"message\":{\"content\":\"assistant reply\"}}]}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                );
            },
            GetPromptPath()
        );

        var result = await service.RunAsync("Summarize the repo");

        result.Should().Be("assistant reply");
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Upstream_Remains_Throttled()
    {
        var service = CreateService(
            (_, _) =>
            {
                var throttledResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(throttledResponse);
            },
            GetPromptPath()
        );

        var act = () => service.RunAsync("Summarize the repo");

        var exception = await act.Should().ThrowAsync<UpstreamServiceException>();
        exception.Which.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Upstream_Returns_Whitespace_Content()
    {
        var service = CreateService(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"choices\":[{\"message\":{\"content\":\"   \"}}]}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                ),
            GetPromptPath()
        );

        var act = () => service.RunAsync("Summarize the repo");

        await act.Should()
            .ThrowAsync<UpstreamServiceException>()
            .WithMessage("*no assistant content*");
    }

    private static RepoAssistantService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        string promptPath
    ) => CreateService(new StubHttpMessageHandler(responder), promptPath);

    private static RepoAssistantService CreateService(
        StubHttpMessageHandler handler,
        string promptPath
    ) => new(new HttpClient(handler), promptPath, NullLogger<RepoAssistantService>.Instance);

    private static string GetPromptPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "prompts",
                "repo-assistant.prompty"
            )
        );
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>
        > _responder;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder
        )
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return _responder(request, cancellationToken);
        }
    }
}
