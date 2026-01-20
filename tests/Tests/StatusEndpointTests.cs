using System.Net;
using System.Net.Http.Json;
using Core.Services;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        // Arrange
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the service with a test version
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IHealthStatusService)
                    );
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    var healthService = new HealthStatusService();
                    healthService.SetDegraded(false); // System is healthy
                    services.AddSingleton<IHealthStatusService>(healthService);
                });
            })
            .CreateClient();

        // Act
        var response = await client.GetAsync("/status");
        var content = await response.Content.ReadFromJsonAsync<StatusResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Status_Should_Return_Degraded_When_System_Is_Degraded()
    {
        // Arrange
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the service with a test version
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IHealthStatusService)
                    );
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    var healthService = new HealthStatusService();
                    healthService.SetDegraded(true); // System is degraded
                    services.AddSingleton<IHealthStatusService>(healthService);
                });
            })
            .CreateClient();

        // Act
        var response = await client.GetAsync("/status");
        var content = await response.Content.ReadFromJsonAsync<StatusResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Status.Should().Be("degraded");
    }
}

public record StatusResponse(string Status);
