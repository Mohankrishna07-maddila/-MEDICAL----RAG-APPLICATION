using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using HealthBot.Api.Models;

namespace HealthBot.Api.Services;

public class DynamoTicketRepository
{
    private readonly IAmazonDynamoDB _client;
    private const string TableName = "SupportTickets";

    public DynamoTicketRepository(IAmazonDynamoDB client)
    {
        _client = client;
    }

    public async Task SaveAsync(SupportTicket ticket)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["TicketId"] = new AttributeValue { S = ticket.TicketId },
            ["CreatedAt"] = new AttributeValue
            {
                N = ticket.CreatedAt.ToString()
            },
            ["SessionId"] = new AttributeValue { S = ticket.SessionId },
            ["Reason"] = new AttributeValue { S = ticket.Reason },
            ["Status"] = new AttributeValue { S = ticket.Status }
        };

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = item
        });
    }
}
