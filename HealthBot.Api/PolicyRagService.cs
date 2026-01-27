namespace HealthBot.Api;

using HealthBot.Api.Models;
using HealthBot.Api.Services;
using System.IO;

public class PolicyRagService
{
    private readonly IAIService _ai;
    private readonly EmbeddingService _embedder;
    private readonly DynamoVectorRepository _repo;
    private readonly S3DocumentLoader _s3; // Inject S3
    private List<VectorChunk> _globalVectors = [];

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<VectorChunk>> _userCache = new();

    public PolicyRagService(
        IAIService ai, 
        EmbeddingService embedder,
        DynamoVectorRepository repo,
        S3DocumentLoader s3)
    {
        _ai = ai;
        _embedder = embedder;
        _repo = repo;
        _s3 = s3;
        
        InitializeAsync().Wait();
    }

    private async Task InitializeAsync()
    {
        // 1. Load existing knowledge from DynamoDB
        _globalVectors = await _repo.GetAllVectorsAsync();
        Console.WriteLine($"[RAG] Loaded {_globalVectors.Count} vectors from DynamoDB.");

        // 2. Sync with S3 (Check for new files)
        try 
        {
            var s3Files = await _s3.ListDocuments();
            if (s3Files.Count == 0) Console.WriteLine("[RAG] S3 appears empty.");

            var newVectors = new List<VectorChunk>();

            foreach (var fileKey in s3Files)
            {
                // Check if we already have this file indexed
                // We assume the format: "Source: {fileKey}\n---"
                bool alreadyIndexed = _globalVectors.Any(v => v.Text.StartsWith($"Source: {fileKey}"));
                
                if (alreadyIndexed)
                {
                    // Console.WriteLine($"[RAG] Skipping {fileKey} (Already indexed)");
                    continue;
                }

                Console.WriteLine($"[RAG] Found NEW file in S3: {fileKey}. Indexing...");
                var content = await _s3.LoadContent(fileKey);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var chunks = Chunk(content);
                foreach (var c in chunks)
                {
                    var emb = await _embedder.EmbedAsync("search_document: " + c);
                    var textWithSource = $"Source: {fileKey}\n---\n{c}";
                    
                    var chunkObj = new VectorChunk { Text = textWithSource, Embedding = emb, SessionId = "GLOBAL" };
                    _globalVectors.Add(chunkObj); // Add to memory
                    newVectors.Add(chunkObj);     // Queue for DB
                }
            }

            // 3. Save only the NEW vectors to DB
            if (newVectors.Count > 0)
            {
                await _repo.SaveVectorsAsync(newVectors);
                Console.WriteLine($"[RAG] Smart Sync Complete. Added {newVectors.Count} new vectors to DynamoDB.");
            }
            else
            {
                Console.WriteLine("[RAG] Smart Sync: No new files to index.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAG] Error during S3 Sync: {ex.Message}");
        }
    }


    public List<VectorChunk> GetAllVectors() => _globalVectors;

    public async Task<string> Answer(string sessionId, string question, List<ChatMessage> history)
    {
        var conversation = string.Join("\n",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var policyContext = await GetContext(sessionId, question);

        // ... (system prompt construction remains the same, just keeping it concise here for diff)
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
“Iam sorry! I’m here to help with health insurance questions. Could you please tell me what you’d like to know?”

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

    public async Task<string> GetContext(string sessionId, string question)
    {
        // 1. Check Cache first
        if (!_userCache.TryGetValue(sessionId, out var userVectors))
        {
             // Cache miss: Fetch from DB (Network Call)
             userVectors = await _repo.GetVectorsBySessionAsync(sessionId);
             _userCache[sessionId] = userVectors; // Store in cache
             Console.WriteLine($"[RAG] Cache Miss for {sessionId}. Fetched {userVectors.Count} vectors.");
        }
        else
        {
             Console.WriteLine($"[RAG] Cache Hit for {sessionId}.");
        }
        
        // 2. Embed the question
        var qEmb = await _embedder.EmbedAsync("search_query: " + question);

        // 3. Compare
        var top = userVectors
            .Select(v => new { Text = v.Text, Score = Cosine(qEmb, v.Embedding) })
            .Where(x => x.Score > 0.45f) 
            .OrderByDescending(x => x.Score)
            .Take(3)
            .Select(v => v.Text);

        return string.Join("\n", top);
    }

    public async Task AddUserPolicy(string sessionId, string policyText)
    {
        // 1. Check existing vectors for this session to avoid duplicates
        var existingVectors = await _repo.GetVectorsBySessionAsync(sessionId);
        
        // 2. We only want to add chunks that are NOT already in the DB
        var chunks = Chunk(policyText);
        var newVectors = new List<VectorChunk>();

        foreach (var c in chunks)
        {
            // Simple check: does this text already exist for this user?
            if (existingVectors.Any(e => e.Text == c))
            {
                Console.WriteLine($"[RAG] Skipping duplicate chunk for session {sessionId}");
                continue;
            }

            var emb = await _embedder.EmbedAsync("search_document: " + c);
            newVectors.Add(new VectorChunk 
            { 
                Text = c, 
                Embedding = emb, 
                SessionId = sessionId 
            });
        }

        if (newVectors.Count > 0)
        {
            await _repo.SaveVectorsAsync(newVectors);
            Console.WriteLine($"[RAG] Added {newVectors.Count} new vectors for session {sessionId}");
            
            // INVALIDATE CACHE so next request fetches fresh data
            _userCache.TryRemove(sessionId, out _);
        }
    }

    public async Task<(string Context, bool Found, double Confidence)> GetDetailedContext(string sessionId, string question)
    {
        if (!_userCache.TryGetValue(sessionId, out var userVectors))
        {
             userVectors = await _repo.GetVectorsBySessionAsync(sessionId);
             _userCache[sessionId] = userVectors;
        }
        var qEmb = await _embedder.EmbedAsync("search_query: " + question);

        var top = userVectors
            .Select(v => new { Text = v.Text, Score = Cosine(qEmb, v.Embedding) })
            .Where(x => x.Score > 0.45f) 
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        if (top.Count == 0)
            return (string.Empty, false, 0.0);

        var maxScore = top.First().Score;
        return (string.Join("\n", top.Select(v => v.Text)), true, maxScore);
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
