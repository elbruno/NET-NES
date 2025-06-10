using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using System.ClientModel;

namespace NET_NES.GameActionProcessor;

public class AoaiGameActionProvider : GameActionProviderBase, IGameActionProvider
{
    public AoaiGameActionProvider(string endpoint, string modelId, string apiKey)
    {
        var credential = new ApiKeyCredential(apiKey);
        IChatClient chatClient =
            new AzureOpenAIClient(new Uri(endpoint), credential)
                    .GetChatClient(modelId)
                    .AsIChatClient();
        chat = chatClient;
    }
}
