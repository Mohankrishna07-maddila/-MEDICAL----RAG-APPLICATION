using System.Text;
using HealthBot.Api.Models;

public class HybridContextService
{
    private readonly DynamoConversationMemory _memory;
    private readonly PolicyRagService _rag;

    public HybridContextService(
        DynamoConversationMemory memory,
        PolicyRagService rag)
    {
        _memory = memory;
        _rag = rag;
    }

    public async Task<string> BuildContext(
        string sessionId,
        string question,
        IntentType intent)
    {
        var context = new StringBuilder();

        // 1️⃣ Conversation context
        var history = await _memory.GetLastMessages(sessionId, 5);
        if (history.Any())
        {
            context.AppendLine("CONVERSATION CONTEXT:");
            foreach (var h in history)
                context.AppendLine($"{h.Role}: {h.Content}");
        }

        // 2️⃣ Policy context (ONLY when relevant)
        if (intent == IntentType.PolicyInfo || intent == IntentType.ClaimProcess)
        {
            var policy = await _rag.GetSemanticContext(question);
            context.AppendLine("\nPOLICY CONTEXT:");
            context.AppendLine(policy);
        }

        return context.ToString();
    }
}
