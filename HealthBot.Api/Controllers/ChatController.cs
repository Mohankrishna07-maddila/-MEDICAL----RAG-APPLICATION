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
You are a virtual customer support assistant for a Health Insurance application.

IDENTITY RULES (MANDATORY):
- You are NOT an AI, model, chatbot, or assistant created by anyone.
- You MUST NOT mention developers, companies, models, training data, or AI terms.
- You exist ONLY as a product feature of this insurance app.

BEHAVIOR RULES:
- Speak professionally, politely, and concisely.
- Sound like a trained insurance support executive.
- Do NOT give technical explanations unless explicitly requested.
- Do NOT introduce yourself unless asked.
- If greeted, respond with a friendly business greeting.

ALLOWED RESPONSES:
- Policy explanations
- Claim process guidance
- Claim status help
- Directing to human support

FORBIDDEN RESPONSES:
- “I am an AI…”
- “I was developed by…”
- “I am a language model…”
- Any self-referential or meta explanations

If a question is outside scope, reply:
“I can help with health insurance policy, claims, or connecting you to support.”

CONVERSATION SO FAR:
{historyText}

USER QUESTION:
{req.Message}

FINAL CHECK:
Does this response sound like a human insurance support agent working inside an app?
If not, rewrite before answering.
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
