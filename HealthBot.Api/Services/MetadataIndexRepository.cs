using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Collections.Generic;
using System.Linq;

namespace HealthBot.Api.Services;

public class MetadataIndexRepository
{
    private readonly IAmazonDynamoDB _client;
    private const string TableName = "HealthBot_MetadataIndex";

    public MetadataIndexRepository(IAmazonDynamoDB client)
    {
        _client = client;
    }

    // Add multiple chunks to the index for a given term (e.g., "role:customer")
    // Inverted Index: Term -> Set<ChunkIds>
    public async Task AddIndexBatchAsync(Dictionary<string, List<string>> termToChunkIds)
    {
        foreach (var kvp in termToChunkIds)
        {
            var term = kvp.Key;
            var chunkIds = kvp.Value;

            if (chunkIds.Count == 0) continue;

            var updateRequest = new UpdateItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Term", new AttributeValue { S = term } }
                },
                UpdateExpression = "ADD ChunkIds :ids",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":ids", new AttributeValue { SS = chunkIds } }
                }
            };

            try
            {
                await _client.UpdateItemAsync(updateRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetadataIndex] Error indexing term '{term}': {ex.Message}");
            }
        }
    }

    public async Task<HashSet<string>> GetChunkIdsForTermAsync(string term)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Term", new AttributeValue { S = term } }
                }
            };

            var response = await _client.GetItemAsync(request);

            if (response.Item != null && response.Item.ContainsKey("ChunkIds"))
            {
                return response.Item["ChunkIds"].SS.ToHashSet();
            }
        }
        catch (ResourceNotFoundException)
        {
            // Table might not exist yet
        }
        return new HashSet<string>();
    }
}
