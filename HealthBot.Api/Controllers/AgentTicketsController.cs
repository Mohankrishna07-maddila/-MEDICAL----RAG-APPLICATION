using Microsoft.AspNetCore.Mvc;
using HealthBot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using HealthBot.Api.Auth;

namespace HealthBot.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("agent/tickets")]
public class AgentTicketsController : ControllerBase
{
    private readonly TicketService _tickets;

    public AgentTicketsController(TicketService tickets)
    {
        _tickets = tickets;
    }

    // LIST OPEN
    [HttpGet("open")]
    public async Task<IActionResult> GetOpenTickets()
    {
        var tickets = await _tickets.ListOpenAsync();
        return Ok(tickets);
    }

    // GET BY ID + CREATEDAT  ✅ THIS IS THE ONE YOU NEED
    [HttpGet("{ticketId}/{createdAt:long}")]
    public async Task<IActionResult> GetTicket(
        string ticketId,
        long createdAt)
    {
        var ticket = await _tickets.GetAsync(ticketId, createdAt);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    // UPDATE STATUS
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
