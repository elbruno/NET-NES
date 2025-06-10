namespace NET_NES.GameActionProcessor;

public interface IGameActionProvider
{
    Task<GameActionResult?> AnalyzeFrameAsync(byte[] imageBytes, string lastAction);
}


