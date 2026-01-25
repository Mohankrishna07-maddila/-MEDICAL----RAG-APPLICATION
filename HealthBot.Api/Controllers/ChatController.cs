using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthBot.Api.Auth;

namespace HealthBot.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.User)]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IAIService _ai;
    private readonly DynamoConversationMemory _memory;
    private readonly TicketService _ticketService;

    public ChatController(
        IAIService ai,
        DynamoConversationMemory memory,
        TicketService ticketService)
    {
        _ai = ai;
        _memory = memory;
        _ticketService = ticketService;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var history = await _memory.GetRecentMessagesAsync(request.SessionId);

        var llmResult = await _ai.AskWithIntentAsync(
            request.Message,
            history
        );

        string answer = llmResult.Answer;
        string? ticketId = null;

        if (llmResult.Intent == "TalkToAgent")
        {
            var ticket = await _ticketService.CreateAsync(
                request.SessionId,
                request.Message
            );

            ticketId = ticket.TicketId;

            answer = $"I've created a support ticket for you. " +
                     $"Your ticket ID is {ticketId}. " +
                     $"A support agent will contact you shortly.";
        }

        await _memory.AddMessageAsync(request.SessionId, "user", request.Message);
        await _memory.AddMessageAsync(request.SessionId, "assistant", answer);

        return Ok(new
        {
            intent = llmResult.Intent,
            answer,
            ticketId
        });
    }
}

public record ChatRequest(string SessionId, string Message);
