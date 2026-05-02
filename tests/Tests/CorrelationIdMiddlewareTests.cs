using Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Should_Generate_CorrelationId_When_Header_Is_Absent()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

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
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = expected;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context
            .Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString()
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task InvokeAsync_Should_Generate_Valid_Guid_When_Header_Is_Absent()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var value = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Guid.TryParse(value, out _).Should().BeTrue("the generated value should be a valid GUID");
    }
}
