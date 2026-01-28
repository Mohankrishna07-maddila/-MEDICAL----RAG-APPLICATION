using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using HealthBot.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HealthBot.Api;

public class DynamoConversationMemory
{
    private readonly IDynamoDBContext _context;
    private readonly IMemoryCache _cache;
    
    // Fallback in-memory storage
    private static readonly Dictionary<string, List<ChatMessage>> _fallbackMemory = new();

    public DynamoConversationMemory(IAmazonDynamoDB client, IMemoryCache cache)
    {
        _context = new DynamoDBContext(client);
        _cache = cache;
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
            
            // Invalidate cache for this session
            for (int i = 1; i <= 10; i++)
            {
                _cache.Remove($"chat_history_{sessionId}_{i}");
            }
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
        // Check cache first
        var cacheKey = $"chat_history_{sessionId}_{limit}";
        if (_cache.TryGetValue<List<ChatMessage>>(cacheKey, out var cachedMessages))
        {
            Console.WriteLine($"[PERF] Cache HIT for session {sessionId}");
            return cachedMessages;
        }

        Console.WriteLine($"[PERF] Cache MISS for session {sessionId} - querying DynamoDB");
        
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

            var result = messages
                .Take(limit)
                .OrderBy(m => m.Timestamp) // Oldest first for chat context
                .ToList();

            // Cache for 5 minutes
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            
            return result;
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

    public async Task AddMessageBatchAsync(string sessionId, params (string role, string content, string messageType, string intent, string source, double confidence, string ticketId)[] messages)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var tasks = new List<Task>();

            foreach (var (role, content, messageType, intent, source, confidence, ticketId) in messages)
            {
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
                    ExpiresAt = now.AddHours(24).ToUnixTimeSeconds()
                };

                tasks.Add(_context.SaveAsync(chatMessage));
                now = now.AddMilliseconds(1); // Ensure unique timestamps
            }

            await Task.WhenAll(tasks);
            
            // Invalidate cache for this session
            for (int i = 1; i <= 10; i++)
            {
                _cache.Remove($"chat_history_{sessionId}_{i}");
            }
            
            Console.WriteLine($"[PERF] Batch saved {messages.Length} messages in parallel");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Batch write failed: {ex.Message}");
            // Fallback to sequential writes
            foreach (var msg in messages)
            {
                await AddMessageAsync(sessionId, msg.role, msg.content, msg.messageType, msg.intent, msg.source, msg.confidence, msg.ticketId);
            }
        }
    }
}
