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
        IntentType intent,
        List<ChatMessage> history = null)  // Accept pre-loaded history
    {
        var context = new StringBuilder();
        bool isLowConfidence = false;

        Console.WriteLine($"[HYBRID] Intent detected: {intent}");

        var isFollowUp = IsFollowUp(question);
        var hasNewConcept = ContainsNewMedicalTerm(question);
        
        // Stricter check: "Explain" alone is NOT enough. It must be "Explain that", "Explain again", etc.
        var isFollowUpExplain = 
            question.Contains("explain again", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("explain that", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("explain it", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("say that again", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("what do you mean", StringComparison.OrdinalIgnoreCase);

        // 1️⃣ Conversation (Check for Frustration)
        // Use provided history or load if not provided
        if (history == null)
        {
            Console.WriteLine("[PERF] Cache MISS - HybridContext loading history");
            history = await _memory.GetLastMessagesAsync(sessionId, 6);
        }
        else
        {
            Console.WriteLine("[PERF] Cache HIT - Using pre-loaded history");
        }
        
        bool isFrustrated = false;
        // Strict keywords - only things that indicate failure/anger
        var struggleKeywords = new[] { "stupid", "useless", "broken", "nothing", "talk to human", "agent", "ticket", "circular" };
        var confusionKeywords = new[] { "understand", "not clear", "confused", "what do you mean" };

        // Check for consecutive confusion
        int consecutiveConfusion = 0;
        foreach (var msg in history.Where(m => m.Role == "user").OrderByDescending(m => m.Timestamp))
        {
            if (confusionKeywords.Any(k => msg.Content.Contains(k, StringComparison.OrdinalIgnoreCase)))
                consecutiveConfusion++;
            else
                break; // Reset if they asked a normal question in between
        }

        bool currentIsConfusion = confusionKeywords.Any(k => question.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (currentIsConfusion) consecutiveConfusion++;

        // Trigger only if heavily frustrated OR confused 3 times in a row
        if (struggleKeywords.Any(k => question.Contains(k, StringComparison.OrdinalIgnoreCase)) || consecutiveConfusion >= 3)
        {
             Console.WriteLine($"[HYBRID] Frustration Detected! Consecutive Confusion: {consecutiveConfusion}");
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
        double confidence = 1.0; // Default for non-RAG
        List<string> sources = new List<string>();
        
        if (question.Length > 5 && 
            (!isFollowUp || (isFollowUp && hasNewConcept)) && 
            (intent == IntentType.PolicyInfo || intent == IntentType.ClaimProcess))
        {
            Console.WriteLine("[HYBRID] Using VECTOR RAG (policy) - Checking Confidence");
            var result = await _rag.GetDetailedContext(sessionId, question);
            confidence = result.Confidence;
            sources = result.Sources;
            
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
                confidence = 0.0;
            }
        }
        else if (isFollowUp)
        {
             Console.WriteLine("[HYBRID] Follow-up detected. Skipping RAG.");
        }

        return new HybridContextResult(context.ToString(), isLowConfidence, isFrustrated, confidence, sources);
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

public record HybridContextResult(string ContextString, bool IsLowConfidence, bool IsFrustrated, double Confidence = 0.0, List<string>? Sources = null);
