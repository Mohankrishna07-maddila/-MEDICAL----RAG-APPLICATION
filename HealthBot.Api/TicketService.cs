using HealthBot.Api.Models;
using HealthBot.Api.Services;

namespace HealthBot.Api;

public class TicketService
{
    private readonly DynamoTicketRepository _repo;

    public TicketService(DynamoTicketRepository repo)
    {
        _repo = repo;
    }

    public async Task<SupportTicket> CreateAsync(
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

        await _repo.SaveAsync(ticket);

        Console.WriteLine($"[TICKET STORED] {ticket.TicketId}");

        return ticket;
    }
}
