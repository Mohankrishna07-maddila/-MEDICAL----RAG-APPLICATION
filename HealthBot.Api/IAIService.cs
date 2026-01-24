namespace HealthBot.Api;

public interface IAIService
{
    Task<string> AskAsync(string message);
}
