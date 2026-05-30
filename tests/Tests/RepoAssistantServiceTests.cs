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
        var promptsDir = GetPromptsDirectory();
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
        var service = CreateService(handler, promptsDir);

        var result = await service.RunAsync(
            "repo-assistant",
            "Summarize the repo",
            fileContext: "StatusController.cs",
            projectArea: "api"
        );

        result.Reply.Should().Be("assistant reply");
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
            GetPromptsDirectory()
        );

        var act = () => service.RunAsync("nonexistent-template", "Summarize the repo");

        await act.Should().ThrowAsync<PromptTemplateNotFoundException>();
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Upstream_Returns_NonSuccess_Status()
    {
        var service = CreateService(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)),
            GetPromptsDirectory()
        );

        var act = () => service.RunAsync("repo-assistant", "Summarize the repo");

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
            GetPromptsDirectory()
        );

        var act = () => service.RunAsync("repo-assistant", "Summarize the repo");

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
            GetPromptsDirectory()
        );

        var act = () => service.RunAsync("repo-assistant", "Summarize the repo");

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
            GetPromptsDirectory()
        );

        var result = await service.RunAsync("repo-assistant", "Summarize the repo");

        result.Reply.Should().Be("assistant reply");
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
            GetPromptsDirectory()
        );

        var act = () => service.RunAsync("repo-assistant", "Summarize the repo");

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
            GetPromptsDirectory()
        );

        var act = () => service.RunAsync("repo-assistant", "Summarize the repo");

        await act.Should()
            .ThrowAsync<UpstreamServiceException>()
            .WithMessage("*no assistant content*");
    }

    [Fact]
    public async Task RunAsync_InjectsFileTreeAndSourceIntoRequest_WhenRepoRootProvided()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"repo-ctx-{Guid.NewGuid():N}");
        var coreDir = Path.Combine(tempRoot, "src", "Core", "Services");
        Directory.CreateDirectory(coreDir);
        await File.WriteAllTextAsync(
            Path.Combine(coreDir, "IMyService.cs"),
            "public interface IMyService { }"
        );

        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(
            async (req, ct) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync(ct);
                return OkResponse("ok");
            }
        );

        var service = CreateService(handler, GetPromptsDirectory(), repoRootPath: tempRoot);
        try
        {
            await service.RunAsync("repo-assistant", "test goal");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        capturedBody.Should().Contain("IMyService.cs");
        capturedBody.Should().Contain("IMyService");
    }

    [Fact]
    public async Task RunAsync_ExcludesBinAndObjFromContext()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"repo-ctx-{Guid.NewGuid():N}");
        var coreDir = Path.Combine(tempRoot, "src", "Core");
        var binDir = Path.Combine(tempRoot, "src", "Core", "bin", "Debug");
        var objDir = Path.Combine(tempRoot, "src", "Core", "obj", "net10.0");
        Directory.CreateDirectory(coreDir);
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(objDir);
        await File.WriteAllTextAsync(Path.Combine(coreDir, "IReal.cs"), "// real");
        await File.WriteAllTextAsync(Path.Combine(binDir, "Compiled.cs"), "// should not appear");
        await File.WriteAllTextAsync(Path.Combine(objDir, "Generated.cs"), "// should not appear");

        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(
            async (req, ct) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync(ct);
                return OkResponse("ok");
            }
        );

        var service = CreateService(handler, GetPromptsDirectory(), repoRootPath: tempRoot);
        try
        {
            await service.RunAsync("repo-assistant", "test");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        capturedBody.Should().Contain("IReal.cs");
        capturedBody.Should().NotContain("Compiled.cs");
        capturedBody.Should().NotContain("Generated.cs");
    }

    [Fact]
    public async Task RunAsync_AppendsCallerContextAfterRepoContext()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"repo-ctx-{Guid.NewGuid():N}");
        var coreDir = Path.Combine(tempRoot, "src", "Core");
        Directory.CreateDirectory(coreDir);
        await File.WriteAllTextAsync(Path.Combine(coreDir, "ISvc.cs"), "// repo content");

        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(
            async (req, ct) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync(ct);
                return OkResponse("ok");
            }
        );

        var service = CreateService(handler, GetPromptsDirectory(), repoRootPath: tempRoot);
        try
        {
            await service.RunAsync(
                "repo-assistant",
                "test",
                fileContext: "CALLER_SPECIFIC_SNIPPET"
            );
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        capturedBody.Should().Contain("ISvc.cs");
        capturedBody.Should().Contain("CALLER_SPECIFIC_SNIPPET");
        capturedBody!
            .IndexOf("ISvc.cs", StringComparison.Ordinal)
            .Should()
            .BeLessThan(capturedBody.IndexOf("CALLER_SPECIFIC_SNIPPET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WorksNormally_WhenNoRepoRootProvided()
    {
        var service = CreateService(
            (_, _) => Task.FromResult(OkResponse("reply without repo context")),
            GetPromptsDirectory()
        );

        var result = await service.RunAsync(
            "repo-assistant",
            "test goal",
            fileContext: "some snippet"
        );

        result.Reply.Should().Be("reply without repo context");
    }

    private static RepoAssistantService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        string promptsDirectory
    ) => CreateService(new StubHttpMessageHandler(responder), promptsDirectory);

    private static RepoAssistantService CreateService(
        StubHttpMessageHandler handler,
        string promptsDirectory,
        string? repoRootPath = null
    ) =>
        new(
            new HttpClient(handler),
            promptsDirectory,
            NullLogger<RepoAssistantService>.Instance,
            repoRootPath
        );

    private static HttpResponseMessage OkResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"choices\":[{{\"message\":{{\"content\":\"{content}\"}}}}]}}",
                Encoding.UTF8,
                "application/json"
            ),
        };

    private static string GetPromptsDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "prompts")
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
