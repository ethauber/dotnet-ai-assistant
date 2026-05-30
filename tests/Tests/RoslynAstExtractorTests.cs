using FluentAssertions;
using Infrastructure.Services;

namespace Tests;

public sealed class RoslynAstExtractorTests
{
    [Fact]
    public void ExtractSummary_ReturnsInterfaceWithMethodSignatures()
    {
        const string source = """
            namespace Core.Services;

            public interface IMyService
            {
                Task<string> DoThingAsync(string input, CancellationToken cancellationToken = default);
                bool IsReady { get; }
            }
            """;

        var result = RoslynAstExtractor.ExtractSummary(source);

        result.Should().Contain("namespace Core.Services");
        result.Should().Contain("interface IMyService");
        result.Should().Contain("DoThingAsync");
        result.Should().Contain("string input");
        result.Should().Contain("IsReady");
    }

    [Fact]
    public void ExtractSummary_ReturnsSealedClassWithBaseTypeAndMembers()
    {
        const string source = """
            namespace Infrastructure.Services;

            public sealed class MyService : IMyService
            {
                private readonly string _field;

                public MyService(string dependency) { }

                public async Task<string> DoThingAsync(string input, CancellationToken ct) => "";

                public bool IsReady => true;
            }
            """;

        var result = RoslynAstExtractor.ExtractSummary(source);

        result.Should().Contain("sealed class MyService");
        result.Should().Contain(": IMyService");
        result.Should().Contain("ctor MyService");
        result.Should().Contain("DoThingAsync");
        result.Should().Contain("IsReady");
        // Private fields should not appear.
        result.Should().NotContain("_field");
    }

    [Fact]
    public void ExtractSummary_ReturnsEnumMembers()
    {
        const string source = """
            namespace Core.Entities;

            public enum RunStatus
            {
                Submitted,
                DraftGenerated,
                Approved,
                Rejected
            }
            """;

        var result = RoslynAstExtractor.ExtractSummary(source);

        result.Should().Contain("enum RunStatus");
        result.Should().Contain("Submitted");
        result.Should().Contain("DraftGenerated");
        result.Should().Contain("Approved");
        result.Should().Contain("Rejected");
    }

    [Fact]
    public void ExtractSummary_ReturnsRecordWithParameters()
    {
        const string source = """
            namespace Core.Entities;

            public sealed record AssistantRun(Guid Id, string UserGoal, RunStatus Status);
            """;

        var result = RoslynAstExtractor.ExtractSummary(source);

        result.Should().Contain("record AssistantRun");
        result.Should().Contain("Guid Id");
        result.Should().Contain("UserGoal");
    }

    [Fact]
    public void ExtractSummary_OmitsMethodBodies()
    {
        const string source = """
            public class Calculator
            {
                public int Add(int a, int b)
                {
                    // Sensitive business logic
                    var secret = 42;
                    return a + b + secret;
                }
            }
            """;

        var result = RoslynAstExtractor.ExtractSummary(source);

        result.Should().Contain("Add");
        result.Should().NotContain("secret");
        result.Should().NotContain("42");
        result.Should().NotContain("Sensitive business logic");
    }

    [Fact]
    public void ExtractSummary_ReturnsEmptyString_ForEmptySource()
    {
        var result = RoslynAstExtractor.ExtractSummary(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSummary_HandlesMultipleTypesInOneFile()
    {
        const string source = """
            namespace Api.Models;

            public sealed class RequestDto
            {
                public string Goal { get; set; } = "";
            }

            public sealed class ResponseDto
            {
                public string Reply { get; set; } = "";
            }
            """;

        var result = RoslynAstExtractor.ExtractSummary(source);

        result.Should().Contain("RequestDto");
        result.Should().Contain("ResponseDto");
        result.Should().Contain("Goal");
        result.Should().Contain("Reply");
    }
}
