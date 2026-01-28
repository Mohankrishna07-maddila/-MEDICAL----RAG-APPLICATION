namespace HealthBot.Api;

using HealthBot.Api.Models;
using HealthBot.Api.Services;
using System.IO;
using System.Linq; // Added for Enumerable methods
using System.Collections.Generic;

public class PolicyRagService
{
    private readonly IAIService _ai;
    private readonly EmbeddingService _embedder;
    private readonly DynamoVectorRepository _repo;
    private readonly MetadataIndexRepository _indexRepo; // [NEW] Metadata Index
    private readonly FakePolicySeeder _seeder;           // [NEW] Fake Data
    private readonly S3DocumentLoader _s3; 
    
    // In-Memory Fallback cache (Optional, but good for performance)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<VectorChunk>> _userCache = new();

    public PolicyRagService(
        IAIService ai, 
        EmbeddingService embedder,
        DynamoVectorRepository repo,
        MetadataIndexRepository indexRepo,
        FakePolicySeeder seeder,
        S3DocumentLoader s3)
    {
        _ai = ai;
        _embedder = embedder;
        _repo = repo;
        _indexRepo = indexRepo;
        _seeder = seeder;
        _s3 = s3;
        
        InitializeAsync().Wait();
    }

    private async Task InitializeAsync()
    {
        Console.WriteLine("[RAG] Initializing Metadata-Driven System...");

        // 1. SEED FAKE DATA (If not already present? For now, we seed every time for demo but check DB first)
        // A better check would be to see if "POL_GOLD" exists in Index.
        var check = await _indexRepo.GetChunkIdsForTermAsync("policy_id:POL_GOLD");
        if (check.Count == 0)
        {
            Console.WriteLine("[RAG] Seeding Fake Policy Data...");
            var policies = _seeder.GeneratePolicies();
            var newVectors = new List<VectorChunk>();
            var indexUpdates = new Dictionary<string, List<string>>(); // term -> [chunkId1, chunkId2]

            foreach (var (text, meta) in policies)
            {
                var chunks = Chunk(text);
                foreach (var c in chunks)
                {
                    var emb = await _embedder.EmbedAsync("search_document: " + c);
                    var chunkObj = new VectorChunk 
                    { 
                        Text = c, 
                        Embedding = emb, 
                        SessionId = "GLOBAL",
                        Metadata = meta 
                    };
                    
                    newVectors.Add(chunkObj);

                    // Prepare Metadata Index updates
                    foreach (var kvp in meta)
                    {
                        var term = $"{kvp.Key}:{kvp.Value}"; // e.g. "role:customer"
                        if (!indexUpdates.ContainsKey(term)) indexUpdates[term] = new List<string>();
                        indexUpdates[term].Add(chunkObj.Id);
                    }
                }
            }

            // Save Vectors
            if (newVectors.Count > 0) await _repo.SaveVectorsAsync(newVectors);
            
            // Save Index
            if (indexUpdates.Count > 0) await _indexRepo.AddIndexBatchAsync(indexUpdates);
            
            Console.WriteLine($"[RAG] Seeded {newVectors.Count} chunks and built Metadata Index.");
        }
        else
        {
             Console.WriteLine("[RAG] Fake Data already seeded.");
        }

        // 2. We could also sync S3 here, but omitting for brevity as requested focus is on Metadata Architecture.
    }

    public async Task<string> Answer(string sessionId, string question, List<ChatMessage> history)
    {
        var conversation = string.Join("\n", history.Select(m => $"{m.Role}: {m.Content}"));
        
        // 1. Determine User Context (In a real app, this comes from JWT/Auth)
        // For Demo: We'll assume the user is a CUSTOMER looking for GOLD/SILVER plans.
        // If the question contains "internal" or "employee", we could simulate an employee role, but let's stick to customer for safety.
        var userContext = new Dictionary<string, string>
        {
            { "role", "customer" } // Hardcoded safety filter
        };

        // 2. Get Context
        var (context, found, _, sources) = await GetDetailedContext(sessionId, question, userContext);
        
        if (!found) return "I'm sorry, I couldn't find any relevant policy information for you.";

        var prompt = $"""
        SYSTEM:
        You are a helpful insurance assistant. Use the provided context to answer. 
        Cite your sources (e.g., [Policy v2]).
        
        CONTEXT:
        {context}
        
        QUESTION:
        {question}
        """;

        return await _ai.GenerateAsync(prompt);
    }
    
    // THE CORE: Step 4 -> 5 -> 6
    public async Task<(string Context, bool Found, double Confidence, List<string> Sources)> GetDetailedContext(
        string sessionId, 
        string question,
        Dictionary<string, string> userFilters = null)
    {
        // Step 1: Embed Query
        var qEmb = await _embedder.EmbedAsync("search_query: " + question);

        // Step 2: Metadata Filtering (The "Sandbox")
        var filters = userFilters ?? new Dictionary<string, string> { { "role", "customer" } };
        
        HashSet<string> candidateIds = null;

        foreach (var kvp in filters)
        {
            var term = $"{kvp.Key}:{kvp.Value}";
            var termIds = await _indexRepo.GetChunkIdsForTermAsync(term);
            
            if (candidateIds == null) candidateIds = termIds; // First filter
            else candidateIds.IntersectWith(termIds); // AND logic
        }

        if (candidateIds == null || candidateIds.Count == 0)
        {
            Console.WriteLine("[RAG] Filter returned 0 candidates. Blocked by Metadata?");
            return (string.Empty, false, 0, new List<string>());
        }
        
        // Step 3: Fetch Actual Vectors (by ID)
        var candidates = await _repo.GetVectorsByIdsAsync(candidateIds);
        Console.WriteLine($"[RAG] Metadata Filter reduced search space to {candidates.Count} chunks.");

        // Step 4: Semantic Search (Cosine Similarity)
        var scoredCandidates = candidates
            .Select(v => new 
            { 
                Chunk = v, 
                SimScore = Cosine(qEmb, v.Embedding) 
            })
            .Where(x => x.SimScore > 0.45f) // Threshold
            .ToList();

        // Step 5: Re-Ranking (Step 6 in Plan)
        // Score = 0.7 * Sim + 0.3 * MetadataConfidence
        var ranked = scoredCandidates
            .Select(x => new
            {
                Item = x,
                FinalScore = (x.SimScore * 0.7) + (double.Parse(x.Chunk.Metadata.GetValueOrDefault("confidence", "0.5")) * 0.3)
            })
            .OrderByDescending(x => x.FinalScore)
            .Take(3)
            .ToList();

        if (ranked.Count == 0) return (string.Empty, false, 0, new List<string>());

        var finalContext = string.Join("\n\n", ranked.Select(r => 
            $"[Source: {r.Item.Chunk.Metadata.GetValueOrDefault("policy_id", "Unknown")}]: {r.Item.Chunk.Text}"));
            
        var distinctSources = ranked.Select(r => r.Item.Chunk.Metadata.GetValueOrDefault("policy_id", "Unknown")).Distinct().ToList();

        return (finalContext, true, ranked.First().FinalScore, distinctSources);
    }

    float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; magA += a[i] * a[i]; magB += b[i] * b[i]; }
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

    // [RESTORED] For Debugging Endpoint
    public List<VectorChunk> GetAllVectors()
    {
        // This is now async in repo, so we block or change controller to async.
        // Changing to blocking for quick fix to match Controller signature.
        return _repo.GetAllVectorsAsync().Result; 
    }

    // [RESTORED] For "Upload Policy" Endpoint
    public async Task AddUserPolicy(string sessionId, string policyText)
    {
        var chunks = Chunk(policyText);
        var newVectors = new List<VectorChunk>();
        
        foreach (var c in chunks)
        {
            var emb = await _embedder.EmbedAsync("search_document: " + c);
            var chunkObj = new VectorChunk 
            { 
                Text = c, 
                Embedding = emb, 
                SessionId = sessionId,
                Metadata = new Dictionary<string, string> 
                { 
                    { "source", "user_upload" },
                    { "created_at", DateTime.UtcNow.ToString("O") }
                }
            };
            newVectors.Add(chunkObj);
        }

        if (newVectors.Count > 0)
        {
            await _repo.SaveVectorsAsync(newVectors);
            // Invalidate cache
            _userCache.TryRemove(sessionId, out _);
        }
    }
}
