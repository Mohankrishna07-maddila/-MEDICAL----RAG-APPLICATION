namespace HealthBot.Api;

using HealthBot.Api.Models;

public interface IAIService
{
    Task<Models.LlmChatResult> AskWithIntentAsync(string question, List<ChatMessage> history);
}
