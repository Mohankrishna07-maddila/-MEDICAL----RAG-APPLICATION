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

    public async Task<HybridContextResult> BuildContext(
        string sessionId,
        string question,
        IntentType intent)
    {
        var context = new StringBuilder();
        bool isLowConfidence = false;

        Console.WriteLine($"[HYBRID] Intent detected: {intent}");

        var isFollowUp = IsFollowUp(question);
        var hasNewConcept = ContainsNewMedicalTerm(question);
        
        var isFollowUpExplain = 
            question.Contains("explain", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("before", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("again", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("understand", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("mean", StringComparison.OrdinalIgnoreCase);

        // 1️⃣ Conversation (Check for Frustration)
        var history = await _memory.GetLastMessagesAsync(sessionId, 10);
        
        bool isFrustrated = false;
        var struggleKeywords = new[] { "understand", "not clear", "mean", "help", "confused", "no use" };
        int struggleCount = history.Count(h => h.Role == "user" && struggleKeywords.Any(k => h.Content.Contains(k, StringComparison.OrdinalIgnoreCase)));
        bool currentIsStruggle = struggleKeywords.Any(k => question.Contains(k, StringComparison.OrdinalIgnoreCase));

        // Trigger only if history has >= 3 struggles AND the current one is also a struggle (total > 3)
        if (currentIsStruggle && struggleCount > 2)
        {
             Console.WriteLine($"[HYBRID] Frustration Detected! History: {struggleCount} + Current");
             isFrustrated = true;
        }

        if (history.Any())
        {
            context.AppendLine("CONVERSATION CONTEXT:");
            foreach (var h in history.TakeLast(5)) // Only show last 5 in context to save tokens
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
                 return new HybridContextResult(context.ToString(), false, isFrustrated);
            }
        }

        // 3️⃣ Policy (Vector RAG)
        if (question.Length > 5 && 
            (!isFollowUp || (isFollowUp && hasNewConcept)) && 
            (intent == IntentType.PolicyInfo || intent == IntentType.ClaimProcess))
        {
            Console.WriteLine("[HYBRID] Using VECTOR RAG (policy) - Checking Confidence");
            var result = await _rag.GetDetailedContext(question);
            
            if (result.Found)
            {
                context.AppendLine("\nPOLICY CONTEXT:");
                context.AppendLine(result.Context);
            }
            else
            {
                // RAG was attempted but nothing found -> Low Confidence
                Console.WriteLine("[HYBRID] Low Confidence: No policy match found.");
                isLowConfidence = true;
            }
        }
        else if (isFollowUp)
        {
             Console.WriteLine("[HYBRID] Follow-up detected. Skipping RAG.");
        }

        return new HybridContextResult(context.ToString(), isLowConfidence, isFrustrated);
    }

    private bool IsFollowUp(string question)
    {
        var q = question.ToLower();
        return q.Contains("that")
            || q.Contains("again")
            || q.Contains("previous")
            || q.Contains("earlier")
            || q.Contains("you said")
            || q.Contains("understand")
            || q.Contains("mean")
            || q.Contains("clarify");
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

public record HybridContextResult(string ContextString, bool IsLowConfidence, bool IsFrustrated);
