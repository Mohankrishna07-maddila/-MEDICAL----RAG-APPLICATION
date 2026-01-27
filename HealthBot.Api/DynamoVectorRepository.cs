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
        // DynamoDB BatchWrite has a limit of 25 items, so we loop individually for simplicity in this demo
        // For production large datasets, use BatchWriteItem
        foreach (var v in vectors)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = Guid.NewGuid().ToString() },
                ["Text"] = new AttributeValue { S = v.Text },
                ["SessionId"] = new AttributeValue { S = v.SessionId ?? "GLOBAL" },
                // Store embedding as a JSON list of numbers (easiest for parsing back)
                ["EmbeddingJson"] = new AttributeValue { S = JsonSerializer.Serialize(v.Embedding) }
            };

            await _client.PutItemAsync(TableName, item);
        }
    }

    public async Task<List<VectorChunk>> GetAllVectorsAsync()
    {
        try
        {
            var request = new ScanRequest { TableName = TableName };
            var response = await _client.ScanAsync(request);

            var list = new List<VectorChunk>();
            foreach (var item in response.Items)
            {
                list.Add(new VectorChunk
                {
                    Text = item["Text"].S,
                    SessionId = item.ContainsKey("SessionId") ? item["SessionId"].S : "GLOBAL",
                    // Deserialize the JSON string back to float[]
                    Embedding = JsonSerializer.Deserialize<float[]>(item["EmbeddingJson"].S) ?? []
                });
            }
            return list;
        }
        catch (ResourceNotFoundException)
        {
            // Table doesn't exist or is empty
            return new List<VectorChunk>();
        }
    }
}
