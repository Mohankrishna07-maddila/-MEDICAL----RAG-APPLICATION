namespace HealthBot.Api;

using HealthBot.Api.Models;
using System.IO;

public class PolicyRagService
{
    private readonly IAIService _ai;
    private readonly List<string> _policyChunks;

    public PolicyRagService(IAIService ai)
    {
        _ai = ai;
        // Using KNOWLEDGEBASE as established in project structure
        var policyText = File.ReadAllText("KNOWLEDGEBASE/policy.txt");
        _policyChunks = ChunkText(policyText);
    }

    public async Task<string> Answer(string question, List<ChatMessage> history)
    {
        var conversation = string.Join("\n",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var policyContext = string.Join("\n", _policyChunks.Take(3));

        var prompt = $"""
You are a health insurance assistant.

CONVERSATION SO FAR:
{conversation}

POLICY:
{policyContext}

INSTRUCTIONS:
- Answer using conversation context if the question refers to previous discussion.
- Answer using policy if it is a policy question.
- If neither applies, say you don't have that information.

USER QUESTION:
{question}
""";

        return await _ai.GenerateAsync(prompt);
    }

    private static List<string> ChunkText(string text, int chunkSize = 500)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
        }
        return chunks;
    }
}
