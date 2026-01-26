using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HealthBot.TestRag;

// Mocks/Copies
public class VectorChunk
{
    public string Text { get; set; } = "";
    public float[] Embedding { get; set; } = [];
}

public class EmbeddingService
{
    private readonly HttpClient _http = new();

    public async Task<float[]> EmbedAsync(string text)
    {
        try 
        {
            var req = new
            {
                model = "nomic-embed-text",
                prompt = text
            };

            var res = await _http.PostAsync(
                "http://localhost:11434/api/embeddings",
                new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json")
            );

            res.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("embedding")
                .EnumerateArray()
                .Select(x => x.GetSingle())
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Embedding failed: {ex.Message}");
            return Array.Empty<float>();
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting RAG Test...");
        
        // 1. Setup Policy
        var policyPath = Path.Combine("..", "HealthBot.Api", "KNOWLEDGEBASE", "policy.txt");
        if (!File.Exists(policyPath))
        {
            Console.WriteLine($"Policy file not found at: {policyPath}");
            return;
        }
        var policyText = File.ReadAllText(policyPath);
        Console.WriteLine($"Policy loaded. Length: {policyText.Length}");

        // 2. Setup Embedder
        var embedder = new EmbeddingService();
        
        // 3. Chunk
        var chunks = Chunk(policyText);
        Console.WriteLine($"Chunks created: {chunks.Count}");

        // 4. Embed Chunks
        var vectors = new List<VectorChunk>();
        foreach (var c in chunks)
        {
            Console.Write(".");
            var emb = await embedder.EmbedAsync("search_document: " + c);
            if (emb.Length == 0) continue;
            vectors.Add(new VectorChunk { Text = c, Embedding = emb });
        }
        Console.WriteLine("\nAll chunks embedded.");

        // 5. Test Query
        string question = "what is the claim process";
        
        var qEmb = await embedder.EmbedAsync("search_query: " + question);

        // 6. Cosine Similarity
        var results = vectors
            .Select(v => new { Chunk = v, Score = Cosine(qEmb, v.Embedding) })
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Query: {question}");
        sb.AppendLine($"Query Emb Len: {qEmb.Length}");
        if (qEmb.Length > 5) sb.AppendLine($"Emb Sample: [{qEmb[0]:F4}, {qEmb[1]:F4}, ...]");

        sb.AppendLine("\nTop Matches:");
        foreach (var r in results)
        {
            sb.AppendLine($"Score: {r.Score:F4} | Text: {r.Chunk.Text.Substring(0, Math.Min(50, r.Chunk.Text.Length))}...");
        }
        
        File.WriteAllText("results.txt", sb.ToString());
        Console.WriteLine("\nDone. Wrote results.txt");
    }

    static float Cosine(float[] a, float[] b)
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

    static List<string> Chunk(string text, int chunkSize = 1000, int overlap = 200)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += (chunkSize - overlap))
        {
            if (i > 0) 
            {
                // Ensure we don't start in the middle of nowhere if possible? 
                // Simple sliding window is fine for now.
            }
            chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
        }
        return chunks;
    }
}
