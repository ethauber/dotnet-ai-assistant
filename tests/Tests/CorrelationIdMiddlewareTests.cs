using Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Tests;

public sealed class CorrelationIdMiddlewareTests
{
    // xUnit constructor = beforeEach: runs before every test
    private readonly CorrelationIdMiddleware _middleware = new(_ => Task.CompletedTask);

    private static DefaultHttpContext CreateContext(string? correlationId = null)
    {
        var context = new DefaultHttpContext();
        if (correlationId is not null)
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_Should_Generate_CorrelationId_When_Header_Is_Absent()
    {
        var context = CreateContext();

        await _middleware.InvokeAsync(context);

        context
            .Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString()
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_Should_Preserve_CorrelationId_From_Request_Header()
    {
        var expected = "my-trace-abc123";
        var context = CreateContext(expected);

        await _middleware.InvokeAsync(context);

        context
            .Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString()
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task InvokeAsync_Should_Generate_Valid_Guid_When_Header_Is_Absent()
    {
        var context = CreateContext();

        await _middleware.InvokeAsync(context);

        var value = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Guid.TryParse(value, out _).Should().BeTrue("the generated value should be a valid GUID");
    }
}
