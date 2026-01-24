using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;

namespace HealthBot.Api;

public class DynamoConversationMemory
{
    private readonly AmazonDynamoDBClient? _client;
    private const string TableName = "ChatHistory";
    
    // Fallback in-memory storage
    private static readonly Dictionary<string, List<ChatMessage>> _fallbackMemory = new();

    public DynamoConversationMemory(IConfiguration config)
    {
        var region = config["AWS:Region"];
        _client = new AmazonDynamoDBClient(
            Amazon.RegionEndpoint.GetBySystemName(region)
        );
    }

    public async Task AddMessageAsync(string sessionId, string role, string content)
    {
        if (_client != null)
        {
            try
            {
                var item = new Dictionary<string, AttributeValue>
                {
                    ["SessionId"] = new AttributeValue { S = sessionId },
                    ["Timestamp"] = new AttributeValue { N = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() },
                    ["Role"] = new AttributeValue { S = role },
                    ["Content"] = new AttributeValue { S = content }
                };

                await _client.PutItemAsync(new PutItemRequest
                {
                    TableName = TableName,
                    Item = item
                });
                return; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DynamoDB write failed: {ex.Message}. Switching to in-memory.");
                // Fall through to fallback
            }
        }

        // Fallback implementation
        if (!_fallbackMemory.ContainsKey(sessionId))
        {
            _fallbackMemory[sessionId] = new List<ChatMessage>();
        }
        _fallbackMemory[sessionId].Add(new ChatMessage(role, content));
    }

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(string sessionId, int limit = 5)
    {
        if (_client != null)
        {
            try
            {
                var request = new QueryRequest
                {
                    TableName = TableName,
                    KeyConditionExpression = "SessionId = :sid",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":sid"] = new AttributeValue { S = sessionId }
                    },
                    ScanIndexForward = false,
                    Limit = limit
                };

                var response = await _client.QueryAsync(request);

                return response.Items
                    .OrderBy(i => long.Parse(i["Timestamp"].N))
                    .Select(i => new ChatMessage(
                        i["Role"].S,
                        i["Content"].S))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DynamoDB read failed: {ex.Message}. Internal Error or Table Missing.");
                // Fall through to fallback
            }
        }

        // Fallback implementation
        if (_fallbackMemory.TryGetValue(sessionId, out var messages))
        {
            return messages.TakeLast(limit).ToList();
        }
        return new List<ChatMessage>();
    }
}
