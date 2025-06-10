using Microsoft.Extensions.AI;

namespace NET_NES.GameActionProcessor;

public class GameActionProviderBase
{
    public IChatClient chat;
    public readonly string promptTemplate = @"Act as a video game player, with high expertise playing Ms Pacman.
Your job is to analyze the current game frame, use the last performed action, and define the next step to be taken to win the game.
The game is Ms Pacman, so the only possible actions are: up, down, left, right.
If there is no available action return undefined.
===
The last action performed was: '{0}'.
If the game is on the start screen or waiting for user input to start, do not suggest any action, just return undefined.
===
The output should be a JSON object with 2 fields: 'nextaction', and 'explanation'.
===
Sample JSON output :
'{{ 'nextaction': 'right', 'explanation': 'Moving right will allow Ms Pacman to collect more pellets while avoiding nearby ghosts.' }}'
'{{ 'nextaction': 'left', 'explanation': 'Moving left will help Ms Pacman avoid an approaching ghost.' }}'
'{{ 'nextaction': 'up', 'explanation': 'Moving up will open up routes to collect more pellets.' }}'";

    public async virtual Task<GameActionResult?> AnalyzeFrameAsync(byte[] imageBytes, string lastAction)
    {
        var imageBytesEnumerable = new List<IEnumerable<byte>> { imageBytes };
        string prompt = string.Format(promptTemplate, lastAction);
        string llmResponse = string.Empty;

        AIContent aic = new DataContent(imageBytes, "image/jpeg");
        List<ChatMessage> messages = new()
        {
            new(ChatRole.User, prompt),
            new(ChatRole.User, [aic])
        };

        var completionUpdates = await chat.GetResponseAsync(messages);
        llmResponse = completionUpdates.Text;

        llmResponse = CleanLlmJsonResponse(llmResponse);
        return System.Text.Json.JsonSerializer.Deserialize<GameActionResult>(llmResponse);
    }

    public string CleanLlmJsonResponse(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return llmResponse;
        var lines = llmResponse.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int start = System.Array.FindIndex(lines, l => l.TrimStart().StartsWith("{"));
        int end = System.Array.FindLastIndex(lines, l => l.TrimEnd().EndsWith("}"));
        string json = null;
        if (start >= 0 && end >= start)
        {
            var jsonLines = lines[start..(end + 1)];
            json = string.Join("\n", jsonLines).Trim();
        }
        else
        {
            json = llmResponse.Trim();
        }
        int lastBrace = json.LastIndexOf('}');
        if (lastBrace >= 0 && lastBrace < json.Length - 1)
        {
            json = json.Substring(0, lastBrace + 1);
        }
        while (json.Length > 0 && json[^1] != '}')
        {
            json = json.Substring(0, json.Length - 1).TrimEnd();
        }
        if (json.EndsWith("}."))
        {
            json = json.Substring(0, json.Length - 1);
        }
        return json;
    }
}
