using Amazon.BedrockRuntime.Model;

namespace Infrastructure;

public class BedrockConverseAdapter
{
    public ConverseRequest BuildConverseRequest(string prompt, string modelId)
    {
        var request = new ConverseRequest
        {
            ModelId = modelId,
            Messages = new List<Message>
            {
                new Message
                {
                    Role = "user",
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Text = prompt
                        }
                    }
                }
            },
            InferenceConfig = new InferenceConfiguration
            {
                Temperature = 0
            }
        };

        return request;
    }
}
