using System.Net;
using System.Net.Http.Json;
using Api.Models;
using Core.Entities;
using Core.Services;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Tests;

public sealed class AssistantRunsControllerTests
    : IClassFixture<WebApplicationFactory<Program>>,
        IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IRepoAssistantService> _repoAssistant = new();
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"AssistantTest-{Guid.NewGuid()}.db"
    );

    public AssistantRunsControllerTests(WebApplicationFactory<Program> factory)
    {
        _repoAssistant
            .Setup(s =>
                s.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PromptRunResult("stub draft", "test0000"));

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the SQLite path with an isolated temp database per test class run
                var descriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<AssistantDbContext>)
                        || d.ServiceType == typeof(AssistantDbContext)
                    )
                    .ToList();
                foreach (var d in descriptors)
                    services.Remove(d);

                services.AddDbContext<AssistantDbContext>(options =>
                    options.UseSqlite($"DataSource={_dbPath}")
                );

                // Replace LLM service with stub so tests don't call Ollama
                var repoDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IRepoAssistantService)
                );
                if (repoDescriptor != null)
                    services.Remove(repoDescriptor);

                services.AddSingleton(_repoAssistant.Object);
            });
        });
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Returns201_WithRunInSubmittedStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/assistant-runs",
            new AssistantRunRequest("Summarize the API", null, null)
        );
        var body = await response.Content.ReadFromJsonAsync<AssistantRunResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        body!.Status.Should().Be(nameof(AssistantRunStatus.Submitted));
        body.UserGoal.Should().Be("Summarize the API");
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Returns404_WhenRunDoesNotExist()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/assistant-runs/{Guid.NewGuid()}");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        problem.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_Returns200_WhenRunExists()
    {
        var client = _factory.CreateClient();
        var id = await CreateRunAsync(client);

        var response = await client.GetAsync($"/assistant-runs/{id}");
        var body = await response.Content.ReadFromJsonAsync<AssistantRunResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Id.Should().Be(id);
    }

    // ── CorrelationId ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AllRequests_ReturnCorrelationIdHeader()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/assistant-runs",
            new AssistantRunRequest("Check correlation", null, null)
        );

        response.Headers.Should().ContainKey("X-Correlation-Id");
        response.Headers.GetValues("X-Correlation-Id").Single().Should().NotBeEmpty();
    }

    [Fact]
    public async Task AllRequests_EchoForwardedCorrelationId()
    {
        var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/assistant-runs/{Guid.NewGuid()}");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be(correlationId);
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_Returns409_WhenRunIsNotInReviewState()
    {
        var client = _factory.CreateClient();
        var id = await CreateRunAsync(client); // status = Submitted

        var response = await client.PostAsync($"/assistant-runs/{id}/approve", null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem!.Title.Should().Be("Invalid state transition");
    }

    [Fact]
    public async Task Approve_Returns200_WhenRunIsInReviewState()
    {
        var client = _factory.CreateClient();
        var id = await CreateAndGenerateDraftAsync(client);

        var response = await client.PostAsync($"/assistant-runs/{id}/approve", null);
        var body = await response.Content.ReadFromJsonAsync<AssistantRunResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be(nameof(AssistantRunStatus.Approved));
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_Returns409_WhenRunIsNotInReviewState()
    {
        var client = _factory.CreateClient();
        var id = await CreateRunAsync(client); // status = Submitted

        var response = await client.PostAsJsonAsync(
            $"/assistant-runs/{id}/reject",
            new RejectRequest(null)
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem!.Title.Should().Be("Invalid state transition");
    }

    // ── EditAndApprove ────────────────────────────────────────────────────────

    [Fact]
    public async Task EditAndApprove_Returns200_WhenRunIsInReviewState()
    {
        var client = _factory.CreateClient();
        var id = await CreateAndGenerateDraftAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/assistant-runs/{id}/edit-and-approve",
            new EditAndApproveRequest("human edited output")
        );
        var body = await response.Content.ReadFromJsonAsync<AssistantRunResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be(nameof(AssistantRunStatus.Revised));
        body.FinalOutput.Should().Be("human edited output");
    }

    // ── Regenerate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Regenerate_Returns409_WhenRunIsAlreadyApproved()
    {
        var client = _factory.CreateClient();
        var id = await CreateAndGenerateDraftAsync(client);
        await client.PostAsync($"/assistant-runs/{id}/approve", null);

        var response = await client.PostAsync($"/assistant-runs/{id}/regenerate", null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem!.Title.Should().Be("Invalid state transition");
    }

    // ── GenerateDraft ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateDraft_Returns404_WhenRunDoesNotExist()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/assistant-runs/{Guid.NewGuid()}/generate-draft",
            null
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        problem!.Title.Should().Be("Run not found");
    }

    // ── ListRecent ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListRecent_Returns200_WithCreatedRuns()
    {
        var client = _factory.CreateClient();
        await CreateRunAsync(client);
        await CreateRunAsync(client);

        var response = await client.GetAsync("/assistant-runs");
        var body = await response.Content.ReadFromJsonAsync<List<AssistantRunResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateRunAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/assistant-runs",
            new AssistantRunRequest("Test goal", null, null)
        );
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AssistantRunResponse>();
        return body!.Id;
    }

    private static async Task<Guid> CreateAndGenerateDraftAsync(HttpClient client)
    {
        var id = await CreateRunAsync(client);
        var draftResponse = await client.PostAsync($"/assistant-runs/{id}/generate-draft", null);
        draftResponse.EnsureSuccessStatusCode();
        return id;
    }
}
