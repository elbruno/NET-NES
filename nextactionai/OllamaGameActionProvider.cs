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
        //chat = new Chat(ollama);
        //chat.Model = model;

        chat = new OllamaApiClient(uri, model);

    }
}
