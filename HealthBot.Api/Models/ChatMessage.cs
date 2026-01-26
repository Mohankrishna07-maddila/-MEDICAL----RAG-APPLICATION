using Amazon.DynamoDBv2.DataModel;

namespace HealthBot.Api.Models;

[DynamoDBTable("ChatHistory")]
public class ChatMessage
{
    [DynamoDBHashKey]
    public string SessionId { get; set; } = default!;

    [DynamoDBRangeKey]
    public long Timestamp { get; set; }

    [DynamoDBProperty]
    public string Role { get; set; } = default!; // user / assistant

    [DynamoDBProperty]
    public string Content { get; set; } = default!;

    [DynamoDBProperty]
    public string MessageType { get; set; } = ""; // GREETING, ANSWER, etc.

    [DynamoDBProperty]
    public string Intent { get; set; } = "";

    [DynamoDBProperty]
    public string Source { get; set; } = ""; // VECTOR_RAG, LLM, etc.

    [DynamoDBProperty]
    public double Confidence { get; set; } // 0.0 - 1.0

    [DynamoDBProperty]
    public string TicketId { get; set; } = "";

    [DynamoDBProperty(AttributeName = "ExpiresAt")]
    public long ExpiresAt { get; set; } // TTL
}
