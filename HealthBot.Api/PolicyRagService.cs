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
        // Auto-seeding disabled. Use ResetAndIngestFromS3Async via controller.
        await Task.CompletedTask;
    }

    public async Task ClearAllDataAsync()
    {
        await _repo.DeleteAllVectorsAsync();
        await _indexRepo.DeleteAllIndexesAsync();
    }

    public async Task ResetAndIngestFromS3Async()
    {
        Console.WriteLine("[RAG] Clearing existing data...");
        await ClearAllDataAsync();

        Console.WriteLine("[RAG] Fetching documents from S3...");
        var files = await _s3.ListDocuments();
        Console.WriteLine($"[RAG] Found {files.Count} files in S3.");
        
        var newVectors = new List<VectorChunk>();
        var indexUpdates = new Dictionary<string, List<string>>();

        foreach (var file in files)
        {
             var content = await _s3.LoadContent(file);
             if (string.IsNullOrWhiteSpace(content)) continue;

             var policyId = Path.GetFileNameWithoutExtension(file);
             
             // Smart Confidence Assignment based on document type
             var confidence = DetermineConfidence(policyId, content);
             
             // Extract user_id from filename for user-specific filtering
             var userId = ExtractUserIdFromFilename(policyId);
             
             var metadata = new Dictionary<string, string> 
             {
                 { "policy_id", policyId },
                 { "user_id", userId },  // NEW: User-specific filter
                 { "role", "customer" }, 
                 { "source", "s3_import" },
                 { "confidence", confidence.ToString("F2") }
             };

             var chunks = Chunk(content);
             foreach (var c in chunks)
             {
                 // Console.WriteLine($"Embedding chunk for {policyId}...");
                 var emb = await _embedder.EmbedAsync("search_document: " + c);
                 var chunkObj = new VectorChunk 
                 { 
                     Text = c, 
                     Embedding = emb, 
                     SessionId = "GLOBAL",
                     Metadata = metadata 
                 };
                 newVectors.Add(chunkObj);
                 
                 foreach (var kvp in metadata)
                 {
                     var term = $"{kvp.Key}:{kvp.Value}";
                     if (!indexUpdates.ContainsKey(term)) indexUpdates[term] = new List<string>();
                     indexUpdates[term].Add(chunkObj.Id);
                 }
             }
        }

        if (newVectors.Count > 0) 
        {
            await _repo.SaveVectorsAsync(newVectors);
            await _indexRepo.AddIndexBatchAsync(indexUpdates);
        }
        Console.WriteLine($"[RAG] Ingestion complete. {newVectors.Count} chunks indexed.");
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

        // Step 2: User-Specific Metadata Filtering
        var userId = ExtractUserIdFromSession(sessionId);
        Console.WriteLine($"[RAG] Session '{sessionId}' mapped to user_id: '{userId}'");
        
        HashSet<string> candidateIds = null;

        if (userId == "global")
        {
            // Demo/test mode: See all documents
            var roleFilter = await _indexRepo.GetChunkIdsForTermAsync("role:customer");
            candidateIds = roleFilter;
            Console.WriteLine($"[RAG] Global session - showing all documents");
        }
        else
        {
            // User-specific mode: Show user's docs + global docs (OR logic)
            var userDocs = await _indexRepo.GetChunkIdsForTermAsync($"user_id:{userId}");
            var globalDocs = await _indexRepo.GetChunkIdsForTermAsync("user_id:global");
            
            candidateIds = new HashSet<string>(userDocs);
            candidateIds.UnionWith(globalDocs); // OR logic: user docs + global docs
            
            Console.WriteLine($"[RAG] User-specific filter: {userDocs.Count} user docs + {globalDocs.Count} global docs = {candidateIds.Count} total");
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

        Console.WriteLine($"\n[RAG] --------------------------------------------------");
        Console.WriteLine($"[RAG] CITATION REPORT: Selected {ranked.Count} chunks");
        foreach (var r in ranked)
        {
            var m = r.Item.Chunk.Metadata;
            Console.WriteLine($"[RAG] >> Source: {m.GetValueOrDefault("policy_id", "Unknown")} (v{m.GetValueOrDefault("version", "?")})");
            Console.WriteLine($"       Score: {r.FinalScore:F4} (Sim: {r.Item.SimScore:F4}, Conf: {m.GetValueOrDefault("confidence", "0.5")})");
            Console.WriteLine($"       Snippet: {r.Item.Chunk.Text.Replace("\n", " ").Substring(0, Math.Min(60, r.Item.Chunk.Text.Length))}...");
        }
        Console.WriteLine($"[RAG] --------------------------------------------------\n");

        var finalContext = string.Join("\n\n", ranked.Select(r => 
            $"[Source: {r.Item.Chunk.Metadata.GetValueOrDefault("policy_id", "Unknown")}]: {r.Item.Chunk.Text}"));
            
        var distinctSources = ranked.Select(r => r.Item.Chunk.Metadata.GetValueOrDefault("policy_id", "Unknown")).Distinct().ToList();

        return (finalContext, true, ranked.First().FinalScore, distinctSources);
    }

    private string ExtractUserIdFromFilename(string filename)
    {
        // user1_policy_details → user1
        // user2_claim_history → user2
        // insurance_faq → global (shared document)
        // support_ticket_logs → global
        
        if (filename.StartsWith("user", StringComparison.OrdinalIgnoreCase))
        {
            var parts = filename.Split('_');
            if (parts.Length > 0)
            {
                return parts[0].ToLower(); // "user1", "user2", "user3"
            }
        }
        
        return "global"; // Shared documents accessible to all users
    }

    private string ExtractUserIdFromSession(string sessionId)
    {
        // Handle multiple formats:
        // "user-1" → "user1"
        // "user1" → "user1"
        // "user-2" → "user2"
        // "user2" → "user2"
        // "ui-session" → "global" (demo mode)
        
        var lower = sessionId.ToLower();
        
        // Check for "user-X" format (with hyphen)
        if (lower.StartsWith("user-"))
        {
            return lower.Replace("-", ""); // "user-1" → "user1"
        }
        
        // Check for "userX" format (without hyphen)
        if (lower.StartsWith("user") && lower.Length > 4 && char.IsDigit(lower[4]))
        {
            return lower; // "user1" → "user1"
        }
        
        return "global"; // Demo/test sessions see all documents
    }

    private double DetermineConfidence(string fileName, string content)
    {
        var lowerFileName = fileName.ToLower();
        
        // Official policy documents (highest confidence)
        if (lowerFileName.Contains("policy_details") || lowerFileName.Contains("corporate_policy"))
            return 1.0;
        
        // Claim history (high confidence - official records)
        if (lowerFileName.Contains("claim_history") || lowerFileName.Contains("claim"))
            return 0.95;
        
        // FAQs (good confidence but not official policy)
        if (lowerFileName.Contains("faq") || content.Contains("Frequently Asked Questions"))
            return 0.85;
        
        // Support tickets/logs (lower confidence - anecdotal)
        if (lowerFileName.Contains("ticket") || lowerFileName.Contains("support") || lowerFileName.Contains("log"))
            return 0.70;
        
        // Default for unknown types
        return 0.80;
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

    public async Task<object> GetDiagnosticInfoAsync()
    {
        var vectors = await _repo.GetAllVectorsAsync();
        var roleCustomerIds = await _indexRepo.GetChunkIdsForTermAsync("role:customer");
        
        var vectorSample = vectors.Take(3).Select(v => new 
        { 
            Id = v.Id, 
            SessionId = v.SessionId, 
            Preview = v.Text.Length > 50 ? v.Text.Substring(0, 50) : v.Text, 
            Metadata = v.Metadata 
        }).ToList();
        
        return new
        {
            TotalVectors = vectors.Count,
            VectorSample = vectorSample,
            MetadataIndex_RoleCustomer_Count = roleCustomerIds.Count,
            MetadataIndex_RoleCustomer_Sample = roleCustomerIds.Take(5).ToList()
        };
    }
}
