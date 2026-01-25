namespace HealthBot.Api;

using HealthBot.Api.Models;

public interface IAIService
{
    Task<Models.LlmChatResult> AskWithIntentAsync(string question, List<ChatMessage> history);
    Task<string> GenerateAsync(string prompt);
    IAsyncEnumerable<string> StreamAsync(string prompt);
    Task<string> ClassifyIntentAsync(string message, List<ChatMessage> history);
}
