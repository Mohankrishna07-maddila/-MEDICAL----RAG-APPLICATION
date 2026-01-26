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

    // wrapper for ChatController compatibility
    public async Task<string> CreateTicketAsync(string sessionId, string message)
    {
        var ticket = await CreateAsync(sessionId, message);
        return ticket.TicketId;
    }

    public async Task<SupportTicket?> GetActiveTicket(string sessionId)
    {
        var openTickets = await ListOpenAsync();
        return openTickets.FirstOrDefault(t => t.SessionId == sessionId);
    }
    public Task<List<SupportTicket>> ListOpenAsync()
        => _repo.ListOpenAsync();

    public Task<SupportTicket?> GetAsync(string ticketId, long createdAt)
        => _repo.GetAsync(ticketId, createdAt);

    public Task UpdateStatusAsync(
        string ticketId,
        long createdAt,
        string status)
        => _repo.UpdateStatusAsync(ticketId, createdAt, status);
}
