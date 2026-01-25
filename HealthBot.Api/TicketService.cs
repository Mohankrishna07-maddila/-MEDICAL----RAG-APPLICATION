using HealthBot.Api.Models;

namespace HealthBot.Api;

public class TicketService
{
    public Task<SupportTicket> CreateAsync(
        string sessionId,
        string reason)
    {
        var ticket = new SupportTicket
        {
            TicketId = $"TKT-{Guid.NewGuid():N}".Substring(0, 12),
            SessionId = sessionId,
            Reason = reason,
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // 🔜 Later: persist to DynamoDB / RDS
        Console.WriteLine($"[TICKET CREATED] {ticket.TicketId}");

        return Task.FromResult(ticket);
    }
}
