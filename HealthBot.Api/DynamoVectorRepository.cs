using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;
using HealthBot.Api.Models; // For VectorChunk

namespace HealthBot.Api.Services;

public class DynamoVectorRepository
{
    private readonly IAmazonDynamoDB _client;
    private const string TableName = "VectorStore";

    public DynamoVectorRepository(IAmazonDynamoDB client)
    {
        _client = client;
    }

    public async Task SaveVectorsAsync(List<VectorChunk> vectors)
    {
        foreach (var v in vectors)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = v.Id }, // Use the chunk's ID
                ["Text"] = new AttributeValue { S = v.Text },
                ["SessionId"] = new AttributeValue { S = v.SessionId ?? "GLOBAL" },
                ["EmbeddingJson"] = new AttributeValue { S = JsonSerializer.Serialize(v.Embedding) },
                // Store metadata map for re-ranking
                ["MetadataJson"] = new AttributeValue { S = JsonSerializer.Serialize(v.Metadata) }
            };

            await _client.PutItemAsync(TableName, item);
        }
        Console.WriteLine($"[Dynamo] Successfully saved {vectors.Count} vectors.");
    }

    public async Task<List<VectorChunk>> GetVectorsByIdsAsync(IEnumerable<string> chunkIds)
    {
         var list = new List<VectorChunk>();
         var ids = chunkIds.ToList();
         
         // BatchGetItem has limit of 100 items per request
         for (int i = 0; i < ids.Count; i += 100)
         {
             var batch = ids.Skip(i).Take(100).ToList();
             var keys = batch.Select(id => new Dictionary<string, AttributeValue> { { "Id", new AttributeValue { S = id } } }).ToList();

             var request = new BatchGetItemRequest
             {
                 RequestItems = new Dictionary<string, KeysAndAttributes>
                 {
                     { 
                        TableName, 
                        new KeysAndAttributes { Keys = keys } 
                     }
                 }
             };

             try 
             {
                var response = await _client.BatchGetItemAsync(request);
                if (response.Responses.ContainsKey(TableName))
                {
                    foreach (var item in response.Responses[TableName])
                    {
                        list.Add(MapItem(item));
                    }
                }
             }
             catch(Exception ex)
             {
                 Console.WriteLine($"[Dynamo] BatchGet Error: {ex.Message}");
             }
         }
         
         return list;
    }

    public async Task<List<VectorChunk>> GetAllVectorsAsync()
    {
        try
        {
            var request = new ScanRequest { TableName = TableName };
            var response = await _client.ScanAsync(request);

            return response.Items.Select(MapItem).ToList();
        }
        catch (ResourceNotFoundException)
        {
            return new List<VectorChunk>();
        }
    }
    
    public async Task<List<VectorChunk>> GetVectorsBySessionAsync(string sessionId)
    {
        try
        {
            var request = new ScanRequest 
            { 
                TableName = TableName,
                FilterExpression = "SessionId = :sid OR SessionId = :global OR SessionId = :legacy_global",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":sid", new AttributeValue { S = sessionId } },
                    { ":global", new AttributeValue { S = "GLOBAL" } },
                    { ":legacy_global", new AttributeValue { S = "GLOBAL_POLICY" } }
                }
            };
            
            var response = await _client.ScanAsync(request);
            return response.Items.Select(MapItem).ToList();
        }
        catch (ResourceNotFoundException)
        {
            return new List<VectorChunk>();
        }
    }

    private VectorChunk MapItem(Dictionary<string, AttributeValue> item)
    {
        var chunk = new VectorChunk
        {
            Text = item["Text"].S,
            SessionId = item.ContainsKey("SessionId") ? item["SessionId"].S : "GLOBAL",
            Embedding = JsonSerializer.Deserialize<float[]>(item["EmbeddingJson"].S) ?? []
        };
        
        if (item.ContainsKey("Id")) chunk.Id = item["Id"].S;
        if (item.ContainsKey("MetadataJson")) 
        {
            chunk.Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(item["MetadataJson"].S) ?? new();
        }

        return chunk;
    }
}
