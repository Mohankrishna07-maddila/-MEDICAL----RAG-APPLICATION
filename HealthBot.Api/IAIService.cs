namespace HealthBot.Api;

public interface IAIService
{
    Task<Models.LlmChatResult> AskWithIntentAsync(string question, List<ChatMessage> history);
}
