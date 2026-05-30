using System.Net;
using System.Net.Http.Json;
using Api.Models;
using Core.Exceptions;
using Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

public sealed class RepoAssistantEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RepoAssistantEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Run_Should_Return_Assistant_Reply_When_Service_Succeeds()
    {
        var client = CreateClient(_ => "assistant reply");

        var response = await client.PostAsJsonAsync(
            "/repo-assistant/run",
            new RepoAssistantRequest { UserGoal = "Summarize the API" }
        );
        var content = await response.Content.ReadFromJsonAsync<RepoAssistantResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Reply.Should().Be("assistant reply");
    }

    [Fact]
    public async Task Run_Should_Return_NotFound_When_Prompt_Template_Is_Missing()
    {
        var client = CreateClient(_ =>
            throw new PromptTemplateNotFoundException("missing.prompty")
        );

        var response = await client.PostAsJsonAsync(
            "/repo-assistant/run",
            new RepoAssistantRequest { UserGoal = "Summarize the API" }
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Prompt template not found");
    }

    [Fact]
    public async Task Run_Should_Return_ServiceUnavailable_When_Model_Call_Fails()
    {
        var client = CreateClient(_ => throw new UpstreamServiceException("failure", 502));

        var response = await client.PostAsJsonAsync(
            "/repo-assistant/run",
            new RepoAssistantRequest { UserGoal = "Summarize the API" }
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Assistant service unavailable");
    }

    [Fact]
    public async Task Run_Should_Return_TooManyRequests_When_Model_Call_Is_Throttled()
    {
        var client = CreateClient(_ => throw new UpstreamServiceException("throttled", 429));

        var response = await client.PostAsJsonAsync(
            "/repo-assistant/run",
            new RepoAssistantRequest { UserGoal = "Summarize the API" }
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Assistant service busy");
    }

    private HttpClient CreateClient(Func<string, string> responder)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IRepoAssistantService)
                    );
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton<IRepoAssistantService>(
                        new StubRepoAssistantService(responder)
                    );
                });
            })
            .CreateClient();
    }

    private sealed class StubRepoAssistantService : IRepoAssistantService
    {
        private readonly Func<string, string> _responder;

        public StubRepoAssistantService(Func<string, string> responder)
        {
            _responder = responder;
        }

        public Task<PromptRunResult> RunAsync(
            string templateName,
            string userGoal,
            string? fileContext = null,
            string? projectArea = null,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(new PromptRunResult(_responder(userGoal), "test0000"));
        }
    }
}
