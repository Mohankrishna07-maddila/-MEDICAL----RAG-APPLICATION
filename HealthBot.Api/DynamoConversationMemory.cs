using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using HealthBot.Api.Models;

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

    public async Task AddMessageAsync(string sessionId, string role, string content, string messageType = "", string intent = "")
    {
        if (_client != null)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;

                var item = new Dictionary<string, AttributeValue>
                {
                    ["SessionId"] = new AttributeValue { S = sessionId },

                    ["Timestamp"] = new AttributeValue
                    {
                        N = now.ToUnixTimeMilliseconds().ToString()
                    },

                    ["Role"] = new AttributeValue { S = role },

                    ["Content"] = new AttributeValue { S = content },
                    
                    ["MessageType"] = new AttributeValue { S = messageType ?? "" },
                    
                    ["Intent"] = new AttributeValue { S = intent ?? "" },

                    // 🔥 TTL ATTRIBUTE (MANDATORY)
                    ["ExpiresAt"] = new AttributeValue
                    {
                        N = now.AddHours(24).ToUnixTimeSeconds().ToString()
                    }
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
        _fallbackMemory[sessionId].Add(new ChatMessage 
        { 
            Role = role, 
            Content = content,
            SessionId = sessionId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MessageType = messageType ?? "",
            Intent = intent ?? ""
        });
    }


    public async Task<List<ChatMessage>> GetLastMessagesAsync(
        string sessionId,
        int limit = 5)
    {
        // If DynamoDB unavailable → fallback to in-memory
        if (_client == null)
        {
            if (_fallbackMemory.TryGetValue(sessionId, out var messages))
            {
                return messages
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }
            return new List<ChatMessage>();
        }

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
                .Select(i => new ChatMessage
                {
                    SessionId = i["SessionId"].S,
                    Role = i["Role"].S,
                    Content = i["Content"].S,
                    Timestamp = long.Parse(i["Timestamp"].N),
                    MessageType = i.ContainsKey("MessageType") ? i["MessageType"].S : "",
                    Intent = i.ContainsKey("Intent") ? i["Intent"].S : ""
                })
                .OrderBy(m => m.Timestamp)
                .ToList();
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[ERROR] DynamoDB Query failed: {ex.Message}");
             return new List<ChatMessage>();
        }
    }
}
