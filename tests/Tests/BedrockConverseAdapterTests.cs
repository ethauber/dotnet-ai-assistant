using FluentAssertions;
using Infrastructure;

namespace Tests;

public class BedrockConverseAdapterTests
{
    [Fact]
    public void Request_Should_Have_Strict_Inference_Params()
    {
        // Arrange
        var adapter = new BedrockConverseAdapter();
        var prompt = "Test prompt";
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";

        // Act
        var request = adapter.BuildConverseRequest(prompt, modelId);

        // Assert
        request.InferenceConfig.Should().NotBeNull();
        request.InferenceConfig.Temperature.Should().Be(0);
    }
}
