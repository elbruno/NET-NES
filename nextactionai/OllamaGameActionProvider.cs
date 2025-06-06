using OllamaSharp;
namespace NET_NES.nextactionai;

public class OllamaGameActionProvider : GameActionProviderBase, IGameActionProvider
{
    public OllamaGameActionProvider()
    {
        // Setup Ollama provider
        var model = "gemma3";
        var uri = new Uri("http://localhost:11434");
        
        var ollama = new OllamaApiClient(uri);
        ollama.SelectedModel = model;
        chat = new Chat(ollama);
        chat.Model = model;
    }


    public async Task<GameActionResult?> AnalyzeFrameAsync(byte[] imageBytes, string lastAction)
    {
        var imageBytesEnumerable = new List<IEnumerable<byte>> { imageBytes };
        string prompt = string.Format(promptTemplate, lastAction);
        string llmResponse = string.Empty;
        await foreach (var answerToken in chat.SendAsync(message: prompt, imagesAsBytes: imageBytesEnumerable))
        {
            llmResponse += answerToken;
        }
        llmResponse = CleanLlmJsonResponse(llmResponse);
        return System.Text.Json.JsonSerializer.Deserialize<GameActionResult>(llmResponse);
    }    
}
