using Microsoft.AspNetCore.Mvc;
using HealthBot.Api.Services;

namespace HealthBot.Api.Controllers;

[ApiController]
[Route("agent/tickets")]
public class AgentTicketsController : ControllerBase
{
    private readonly TicketService _tickets;

    public AgentTicketsController(TicketService tickets)
    {
        _tickets = tickets;
    }

    // 1️⃣ List open tickets
    [HttpGet("open")]
    public async Task<IActionResult> GetOpenTickets()
    {
        var tickets = await _tickets.ListOpenAsync();
        return Ok(tickets);
    }

    // 2️⃣ Get ticket by ID and CreatedAt
    [HttpGet("{ticketId}/{createdAt}")]
    public async Task<IActionResult> GetTicket(string ticketId, long createdAt)
    {
        var ticket = await _tickets.GetAsync(ticketId, createdAt);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    // 3️⃣ Update ticket status
    [HttpPut("{ticketId}/status")]
    public async Task<IActionResult> UpdateStatus(
        string ticketId,
        [FromBody] UpdateTicketStatusRequest request)
    {
        await _tickets.UpdateStatusAsync(
            ticketId,
            request.CreatedAt,
            request.Status
        );

        return Ok(new { message = "Status updated" });
    }
}

public record UpdateTicketStatusRequest(
    long CreatedAt,
    string Status
);
