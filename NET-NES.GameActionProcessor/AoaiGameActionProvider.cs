using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;

namespace NET_NES.GameActionProcessor;

public class AoaiGameActionProvider : GameActionProviderBase, IGameActionProvider
{
    public AoaiGameActionProvider()
    {
        var config = new ConfigurationBuilder().AddUserSecrets(typeof(AoaiGameActionProvider).Assembly).Build();
        var endpoint = config["AZURE_OPENAI_ENDPOINT"];
        var modelId = config["AZURE_OPENAI_MODEL"];

        // create client using API Keys  
        var apiKey = config["AZURE_OPENAI_APIKEY"];
        var credential = new ApiKeyCredential(apiKey);

        IChatClient chatClient =
            new AzureOpenAIClient(new Uri(endpoint), credential)
                    .GetChatClient(modelId)
                    .AsIChatClient();
        chat = chatClient;
    }
}
