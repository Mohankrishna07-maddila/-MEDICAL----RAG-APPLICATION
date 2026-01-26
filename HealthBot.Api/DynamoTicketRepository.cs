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
    public async Task<List<SupportTicket>> ListOpenAsync()
    {
        var request = new ScanRequest
        {
            TableName = "SupportTickets",
            FilterExpression = "#s = :open",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#s"] = "Status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":open"] = new AttributeValue { S = "OPEN" }
            }
        };

        var response = await _client.ScanAsync(request);

        return response.Items.Select(Map).ToList();
    }

    public async Task<SupportTicket?> GetAsync(
        string ticketId,
        long createdAt)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = "SupportTickets",
            Key = new Dictionary<string, AttributeValue>
            {
                ["TicketId"] = new AttributeValue { S = ticketId },
                ["CreatedAt"] = new AttributeValue
                {
                    N = createdAt.ToString()
                }
            }
        });

        return response.Item == null || response.Item.Count == 0
            ? null
            : Map(response.Item);
    }

    public async Task UpdateStatusAsync(
        string ticketId,
        long createdAt,
        string status)
    {
        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = "SupportTickets",
            Key = new Dictionary<string, AttributeValue>
            {
                ["TicketId"] = new AttributeValue { S = ticketId },
                ["CreatedAt"] = new AttributeValue { N = createdAt.ToString() }
            },
            UpdateExpression = "SET #s = :status",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#s"] = "Status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new AttributeValue { S = status }
            }
        });
    }

    public async Task<SupportTicket?> GetByTicketIdAsync(string ticketId)
    {
        var request = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "TicketId = :tid",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":tid"] = new AttributeValue { S = ticketId }
            }
        };

        var response = await _client.QueryAsync(request);
        return response.Items.Count == 0 ? null : Map(response.Items[0]);
    }

    private static SupportTicket Map(Dictionary<string, AttributeValue> item)
    {
        return new SupportTicket
        {
            TicketId = item["TicketId"].S!,
            SessionId = item["SessionId"].S!,
            Reason = item["Reason"].S!,
            Status = item["Status"].S!,
            CreatedAt = long.Parse(item["CreatedAt"].N!)
        };
    }
}
