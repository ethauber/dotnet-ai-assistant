using FluentAssertions;
using Infrastructure;

namespace Tests;

public class BedrockConverseAdapterTests
{
    private readonly BedrockConverseAdapter _adapter = new();

    [Fact]
    public void Request_Should_Have_Strict_Inference_Params()
    {
        var request = _adapter.BuildConverseRequest(
            "Test prompt",
            "anthropic.claude-3-sonnet-20240229-v1:0"
        );

        request.InferenceConfig.Should().NotBeNull();
        request.InferenceConfig.Temperature.Should().Be(0);
    }
}
