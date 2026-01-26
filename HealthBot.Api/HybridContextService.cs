namespace HealthBot.Api;

using System;
using System.Text;
using HealthBot.Api.Models;
using System.Linq;

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

        Console.WriteLine($"[HYBRID] Intent detected: {intent}");

        var isFollowUp = IsFollowUp(question);
        var hasNewConcept = ContainsNewMedicalTerm(question);
        
        var isFollowUpExplain = 
            question.Contains("explain", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("before", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("again", StringComparison.OrdinalIgnoreCase);

        // 1️⃣ Conversation
        var history = await _memory.GetLastMessagesAsync(sessionId, 5);
        if (history.Any())
        {
            context.AppendLine("CONVERSATION CONTEXT:");
            foreach (var h in history)
                context.AppendLine($"{h.Role}: {h.Content}");
        }

        // 2️⃣ Special Handling for "Explain Again"
        if (isFollowUpExplain)
        {
            Console.WriteLine("[HYBRID] 'Explain/Again' detected. Searching history for last ANSWER.");
            var lastAnswer = history
                .Where(m => m.Role == "assistant" && m.MessageType == "ANSWER")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            if (lastAnswer != null)
            {
                 Console.WriteLine("[HYBRID] Found previous ANSWER. Using it as context.");
                 context.AppendLine("\nUSER REQUESTS EXPLANATION OF:");
                 context.AppendLine(lastAnswer.Content);
                 return context.ToString(); // Return immediately, skip RAG
            }
        }

        // 3️⃣ Policy (Vector RAG)
        // Logic: Use RAG if it's NOT a follow-up OR if it IS a follow-up but has a new medical concept
        if ((!isFollowUp || (isFollowUp && hasNewConcept)) && 
            (intent == IntentType.PolicyInfo || intent == IntentType.ClaimProcess))
        {
            Console.WriteLine("[HYBRID] Using VECTOR RAG (policy)");
            var policy = await _rag.GetSemanticContext(question);
            context.AppendLine("\nPOLICY CONTEXT:");
            context.AppendLine(policy);
        }
        else if (isFollowUp)
        {
             Console.WriteLine("[HYBRID] Follow-up detected. Skipping RAG.");
        }

        return context.ToString();
    }

    private bool IsFollowUp(string question)
    {
        var q = question.ToLower();
        return q.Contains("that")
            || q.Contains("again")
            || q.Contains("previous")
            || q.Contains("earlier")
            || q.Contains("you said");
    }

    private bool ContainsNewMedicalTerm(string q)
    {
        var lower = q.ToLower();
        var keywords = new[]
        {
            "cataract", "surgery", "hospital", "ayush",
            "maternity", "pre-existing", "waiting period",
            "claim", "coverage", "policy", "insurance", "network", "cashless"
        };
        return keywords.Any(k => lower.Contains(k));
    }
}
