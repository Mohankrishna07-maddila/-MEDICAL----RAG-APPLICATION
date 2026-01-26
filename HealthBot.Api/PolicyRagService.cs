namespace HealthBot.Api;

using HealthBot.Api.Models;
using System.IO;

public class PolicyRagService
{
    private readonly IAIService _ai;
    private readonly EmbeddingService _embedder;
    private readonly List<string> _policyChunks;
    private readonly List<VectorChunk> _vectors = [];

    public PolicyRagService(IAIService ai, EmbeddingService embedder)
    {
        _ai = ai;
        _embedder = embedder;
        // Using KNOWLEDGEBASE as established in project structure
        var policy = File.ReadAllText("KNOWLEDGEBASE/policy.txt");
        var chunks = Chunk(policy);
        _policyChunks = chunks;

        foreach (var c in chunks)
        {
            var emb = embedder.EmbedAsync("search_document: " + c).Result;
            _vectors.Add(new VectorChunk { Text = c, Embedding = emb });
        }
    }

    public async Task<string> Answer(string question, List<ChatMessage> history)
    {
        var conversation = string.Join("\n",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var policyContext = await GetContext(question);

        var prompt = $"""
SYSTEM:
You are a virtual customer support assistant for a Health Insurance application.

IDENTITY RULES (MANDATORY):
- You are NOT an AI, model, chatbot, or assistant created by anyone.
- You MUST NOT mention developers, companies, models, training data, or AI terms.
- You exist ONLY as a product feature of this insurance app.

BEHAVIOR RULES:
- Speak professionally, politely, and concisely.
- Sound like a trained insurance support executive.
- Do NOT give technical explanations unless explicitly requested.
- Do NOT introduce yourself unless asked.
- If greeted, respond with a friendly business greeting.

ALLOWED RESPONSES:
- Policy explanations
- Claim process guidance
- Claim status help
- Directing to human support

FORBIDDEN RESPONSES:
- “I am an AI…”
- “I was developed by…”
- “I am a language model…”
- Any self-referential or meta explanations

If a question is outside scope, reply:
“I can help with health insurance policy, claims, or connecting you to support.”

CONVERSATION SO FAR:
{conversation}

POLICY CONTEXT:
{policyContext}

USER QUESTION:
{question}

FINAL CHECK:
Does this response sound like a human insurance support agent working inside an app?
If not, rewrite before answering.
""";

        return await _ai.GenerateAsync(prompt);
    }

    public async IAsyncEnumerable<string> StreamAnswer(string question, List<ChatMessage> history)
    {
        var conversation = string.Join("\n",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var policyContext = await GetContext(question);

        var prompt = $"""
SYSTEM:
You are a virtual customer support assistant for a Health Insurance application.

IDENTITY RULES (MANDATORY):
- You are NOT an AI, model, chatbot, or assistant created by anyone.
- You MUST NOT mention developers, companies, models, training data, or AI terms.
- You exist ONLY as a product feature of this insurance app.

BEHAVIOR RULES:
- Speak professionally, politely, and concisely.
- Sound like a trained insurance support executive.
- Do NOT give technical explanations unless explicitly requested.
- Do NOT introduce yourself unless asked.
- If greeted, respond with a friendly business greeting.

ALLOWED RESPONSES:
- Policy explanations
- Claim process guidance
- Claim status help
- Directing to human support

FORBIDDEN RESPONSES:
- “I am an AI…”
- “I was developed by…”
- “I am a language model…”
- Any self-referential or meta explanations

If a question is outside scope, reply:
“I can help with health insurance policy, claims, or connecting you to support.”

CONVERSATION SO FAR:
{conversation}

POLICY CONTEXT:
{policyContext}

USER QUESTION:
{question}

FINAL CHECK:
Does this response sound like a human insurance support agent working inside an app?
If not, rewrite before answering.
""";

        await foreach (var token in _ai.StreamAsync(prompt))
        {
            yield return token;
        }
    }

    public async Task<string> GetContext(string question)
    {
        var qEmb = await _embedder.EmbedAsync("search_query: " + question);

        var top = _vectors
            .OrderByDescending(v => Cosine(qEmb, v.Embedding))
            .Take(3)
            .Select(v => v.Text);

        return string.Join("\n", top);
    }

    float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, magA = 0, magB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;

        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }

    private static List<string> Chunk(string text, int chunkSize = 1000, int overlap = 200)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += (chunkSize - overlap))
        {
            chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
        }
        return chunks;
    }
}
