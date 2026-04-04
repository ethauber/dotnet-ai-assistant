using System.Net;
using System.Text;
using Core.Exceptions;
using FluentAssertions;
using Infrastructure.Services;

namespace Tests;

public sealed class RepoAssistantServiceTests
{
    [Fact]
    public async Task RunAsync_Should_Load_Prompt_And_Return_Assistant_Content()
    {
        var promptPath = Path.GetFullPath(
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
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"assistant reply\"}}]}",
                Encoding.UTF8,
                "application/json"
            ),
        });
        var service = new RepoAssistantService(new HttpClient(handler), promptPath);

        var result = await service.RunAsync("Summarize the repo", projectArea: "api");

        result.Should().Be("assistant reply");
    }

    [Fact]
    public async Task RunAsync_Should_Throw_When_Prompt_File_Is_Missing()
    {
        var service = new RepoAssistantService(
            new HttpClient(
                new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))
            ),
            "missing.prompty"
        );

        var act = () => service.RunAsync("Summarize the repo");

        await act.Should().ThrowAsync<PromptTemplateNotFoundException>();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(_responder(request));
        }
    }
}
