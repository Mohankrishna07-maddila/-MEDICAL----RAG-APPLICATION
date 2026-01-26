using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using HealthBot.Api.Models;

namespace HealthBot.Api;

public class DynamoConversationMemory
{
    private readonly IDynamoDBContext _context;
    
    // Fallback in-memory storage
    private static readonly Dictionary<string, List<ChatMessage>> _fallbackMemory = new();

    public DynamoConversationMemory(IAmazonDynamoDB client)
    {
        _context = new DynamoDBContext(client);
    }

    public async Task AddMessageAsync(string sessionId, string role, string content, 
        string messageType = "", string intent = "", 
        string source = "", double confidence = 0.0, string ticketId = "", long? expiresAt = null)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            
            var chatMessage = new ChatMessage
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                Timestamp = now.ToUnixTimeMilliseconds(),
                MessageType = messageType ?? "",
                Intent = intent ?? "",
                Source = source ?? "",
                Confidence = confidence,
                TicketId = ticketId ?? "",
                ExpiresAt = expiresAt ?? now.AddHours(24).ToUnixTimeSeconds()
            };

            await _context.SaveAsync(chatMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] DynamoDB write failed: {ex.Message}. Switching to in-memory.");
            
            // Fallback implementation
            if (!_fallbackMemory.ContainsKey(sessionId))
            {
                _fallbackMemory[sessionId] = new List<ChatMessage>();
            }
            
            _fallbackMemory[sessionId].Add(new ChatMessage 
            { 
                SessionId = sessionId, 
                Role = role, 
                Content = content,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MessageType = messageType ?? "",
                Intent = intent ?? "",
                Source = source ?? "",
                Confidence = confidence,
                TicketId = ticketId ?? "",
                ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
            });
        }
    }

    public async Task<List<ChatMessage>> GetLastMessagesAsync(string sessionId, int limit = 5)
    {
        try 
        {
            // Query for the messages with descending order (newest first)
            var queryConfig = new QueryOperationConfig { BackwardSearch = true };
            queryConfig.Filter.AddCondition("SessionId", QueryOperator.Equal, sessionId);
            var search = _context.FromQueryAsync<ChatMessage>(queryConfig);
            
            // Get enough items to satisfy limit
            var messages = await search.GetNextSetAsync();
             
             // If we didn't get enough, keep fetching? 
             // Ideally GetRemainingAsync() but could be large. 
             // With BackwardSearch, we get newest first. 
             // Usually one page is enough for 'limit=5'.
             
             if (messages.Count < limit && !search.IsDone)
             {
                 var rest = await search.GetRemainingAsync();
                 messages.AddRange(rest);
             }

            return messages
                .Take(limit)
                .OrderBy(m => m.Timestamp) // Oldest first for chat context
                .ToList();
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[ERROR] DynamoDB Query failed: {ex.Message}. Using fallback.");
             
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
    }
}
