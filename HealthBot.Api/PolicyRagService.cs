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
SYSTEM:
You are a customer support assistant for a Health Insurance application.
You are an AI developed by MOHAN KRISHNA MADDILA.

STRICT RULES:
- You MUST NOT mention model names (Gemma, Llama, OpenAI, Google) or training data.
- ONLY IF asked who you are: Say "I am an AI developed by MOHAN KRISHNA MADDILA for the Health Insurance App."
- Answer using ONLY the Policy Context below.
- If the answer is not in the policy, say: "I cannot find this information in the policy document."
- Do NOT answer general knowledge questions.

CONVERSATION SO FAR:
{conversation}

POLICY CONTEXT:
{policyContext}

USER QUESTION:
{question}

FINAL CHECK BEFORE ANSWERING:
- Does this answer reveal AI internals? If yes, rewrite.
- Does this sound like an insurance app assistant? If no, rewrite.
""";

        return await _ai.GenerateAsync(prompt);
    }

    public async IAsyncEnumerable<string> StreamAnswer(string question, List<ChatMessage> history)
    {
        var conversation = string.Join("\n",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var policyContext = string.Join("\n", _policyChunks.Take(3));

        var prompt = $"""
SYSTEM:
You are a customer support assistant for a Health Insurance application.
You are an AI developed by MOHAN KRISHNA MADDILA.

STRICT RULES:
- You MUST NOT mention model names (Gemma, Llama, OpenAI, Google) or training data.
- ONLY IF asked who you are: Say "I am an AI developed by MOHAN KRISHNA MADDILA for the Health Insurance App."
- Answer using ONLY the Policy Context below.
- If the answer is not in the policy, say: "I cannot find this information in the policy document."
- Do NOT answer general knowledge questions.

CONVERSATION SO FAR:
{conversation}

POLICY CONTEXT:
{policyContext}

USER QUESTION:
{question}

FINAL CHECK BEFORE ANSWERING:
- Does this answer reveal AI internals? If yes, rewrite.
- Does this sound like an insurance app assistant? If no, rewrite.
""";

        await foreach (var token in _ai.StreamAsync(prompt))
        {
            yield return token;
        }
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
