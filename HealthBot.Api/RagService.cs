namespace HealthBot.Api;

public class RagService
{
    private readonly IAIService _ai;

    public RagService(IAIService ai)
    {
        _ai = ai;
    }

    public async Task<string> AnswerAsync(
        string userQuestion,
        IntentType intent,
        List<ChatMessage> history
    )
    {
        if (!DocumentStore.Documents.ContainsKey(intent))
            return "No information available.";

        var context = DocumentStore.Documents[intent];

        // Format history for the prompt
        var historyText = string.Join("\n", history.Select(h => $"{h.Role.ToUpper()}: {h.Content}"));

        var prompt = $"""
You are a health insurance support assistant.
Answer the question using ONLY the information below.
If the answer is not in the text, say "I don't know".

Information:
{context}

Recent Conversation:
{historyText}

Question:
{userQuestion}
""";

        return await _ai.AskAsync(prompt);
    }
}
