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
    private readonly PolicyRagService _policyRag;

    public ChatController(
        IAIService ai,
        DynamoConversationMemory memory,
        TicketService ticketService,
        PolicyRagService policyRag)
    {
        _ai = ai;
        _memory = memory;
        _ticketService = ticketService;
        _policyRag = policyRag;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var history = await _memory.GetLastMessagesAsync(request.SessionId, 5);

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
        else if (llmResult.Intent == "PolicyInfo")
        {
            answer = await _policyRag.Answer(request.Message, history);
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

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest req)
    {
        Response.ContentType = "text/plain";
        var fullResponse = new System.Text.StringBuilder();

        // 1. Fetch History
        var history = await _memory.GetLastMessagesAsync(req.SessionId, 5);
        var historyText = string.Join("\n", history.Select(m => $"{m.Role}: {m.Content}"));

        // 2. Classify Intent
        var intent = await _ai.ClassifyIntentAsync(req.Message, history);

        IAsyncEnumerable<string> tokenStream;

        // 3. Route Logic
        if (intent == "PolicyInfo")
        {
            tokenStream = _policyRag.StreamAnswer(req.Message, history);
        }
        else if (intent == "TalkToAgent")
        {
            var ticket = await _ticketService.CreateAsync(req.SessionId, req.Message);
            // Create a simple stream for the static response
            tokenStream = StreamStaticMessage($"I've created a support ticket for you. Ticket ID: {ticket.TicketId}. An agent will contact you shortly.");
        }
        else
        {
            // Unknown / General Chat -> Use Hardened Chat Prompt
            var prompt = $@"SYSTEM:
You are a customer support assistant for a Health Insurance application.
You are an AI developed by MOHAN KRISHNA MADDILA.

STRICT RULES:
- You MUST NOT mention model names (Gemma, Llama, OpenAI, Google) or training data.
- ONLY IF asked who you are or who created you: Say ""I am an AI developed by MOHAN KRISHNA MADDILA for the Health Insurance App.""
- Do NOT mention your identity unprompted.
- You MUST refuse to answer general knowledge questions (e.g. history, science, math, coding) that are not related to health insurance.
- If the user asks a general question (e.g. ""Who invented the bulb?""), say: ""I can only help with health insurance related queries.""
- Keep answers concise and professional.
- Use the Conversation History below to understand context.

CONVERSATION SO FAR:
{historyText}

USER QUESTION:
{req.Message}

FINAL CHECK:
- Did I mention Gemma or Google? If yes, REWRITE.
- Is this a general knowledge question? If yes, REFUSE.
- Did I mention my developer unprompted? If yes, REMOVE it.
";
            tokenStream = _ai.StreamAsync(prompt);
        }

        // 4. Stream response to client
        await foreach (var token in tokenStream)
        {
            await Response.WriteAsync(token);
            await Response.Body.FlushAsync();
            fullResponse.Append(token);
        }

        // Save to memory after streaming is complete
        await _memory.AddMessageAsync(req.SessionId, "user", req.Message);
        await _memory.AddMessageAsync(req.SessionId, "assistant", fullResponse.ToString());
    }

    private async IAsyncEnumerable<string> StreamStaticMessage(string message)
    {
        // Simulate streaming for static content
        var words = message.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(20); // slight typing effect
        }
    }

    [HttpGet("history/{sessionId}")]
    public async Task<IActionResult> GetHistory(string sessionId)
    {
        var messages = await _memory.GetLastMessagesAsync(sessionId);
        return Ok(messages);
    }
}

public record ChatRequest(string SessionId, string Message);
