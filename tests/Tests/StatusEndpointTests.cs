using System.Net;
using System.Net.Http.Json;
using Core.Services;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests;

public class StatusEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StatusEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_Should_Return_Ok_When_System_Is_Healthy()
    {
        var client = CreateClient(isDegraded: false);

        var response = await client.GetAsync("/status");
        var content = await response.Content.ReadFromJsonAsync<StatusResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Status_Should_Return_Degraded_When_System_Is_Degraded()
    {
        var client = CreateClient(isDegraded: true);

        var response = await client.GetAsync("/status");
        var content = await response.Content.ReadFromJsonAsync<StatusResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Status.Should().Be("degraded");
    }

    private HttpClient CreateClient(bool isDegraded)
    {
        return _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHealthStatusService>();
                    var svc = new HealthStatusService();
                    svc.SetDegraded(isDegraded);
                    services.AddSingleton<IHealthStatusService>(svc);
                })
            )
            .CreateClient();
    }
}

public record StatusResponse(string Status);
