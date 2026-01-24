namespace HealthBot.Api;

public class MockService : IAIService
{
    public Task<string> AskAsync(string message)
    {
        return Task.FromResult($"[Mock] Echo: {message}");
    }
}
