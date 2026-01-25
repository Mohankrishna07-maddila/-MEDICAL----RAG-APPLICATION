using System.IO;
using System.Collections.Generic;
using System;

namespace HealthBot.Api;

public static class DocumentStore
{
    public static Dictionary<IntentType, List<string>> Documents = new()
    {
        {
            IntentType.ClaimProcess,
            ChunkText(File.ReadAllText("KNOWLEDGEBASE/claim_process.txt"))
        },
        {
            IntentType.PolicyInfo,
            ChunkText(File.ReadAllText("KNOWLEDGEBASE/policy.txt"))
        }
    };

    public static List<string> ChunkText(string text, int chunkSize = 500)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
        }
        return chunks;
    }
}