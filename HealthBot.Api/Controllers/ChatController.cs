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
    private readonly HybridContextService _hybrid;

    public ChatController(
        IAIService ai,
        DynamoConversationMemory memory,
        TicketService ticketService,
        PolicyRagService policyRag,
        HybridContextService hybrid)
    {
        _ai = ai;
        _memory = memory;
        _ticketService = ticketService;
        _policyRag = policyRag;
        _hybrid = hybrid;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // Single prompt approach for non-streaming as well
        var hybridContext = await _hybrid.BuildContext(request.SessionId, request.Message, IntentType.PolicyInfo);
        
        var prompt = $"""
SYSTEM:
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

{hybridContext}

USER QUESTION:
{request.Message}

INSTRUCTIONS:
- Prefer conversation context for follow-ups.
- Use policy only when relevant.
- Stay within insurance domain.
- FINAL CHECK: Does this response sound like a human insurance support agent working inside an app?
""";

        var answer = await _ai.GenerateAsync(prompt);
        await _memory.AddMessageAsync(request.SessionId, "user", request.Message);
        await _memory.AddMessageAsync(request.SessionId, "assistant", answer);

        return Ok(new
        {
            intent = "General", // Intent is now implicit/ignored by frontend mostly
            answer,
            ticketId = (string?)null
        });
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest req)
    {
        Response.ContentType = "text/plain";
        var fullResponse = new System.Text.StringBuilder();

        // Single Prompt Logic
        // We pass IntentType.PolicyInfo to force RAG context building.
        // The Prompt Instructions ("Use policy only when relevant") handle the suppression.
        var hybridContext = await _hybrid.BuildContext(req.SessionId, req.Message, IntentType.PolicyInfo);

        var prompt = $"""
SYSTEM:
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

{hybridContext}

USER QUESTION:
{req.Message}

INSTRUCTIONS:
- Prefer conversation context for follow-ups.
- Use policy only when relevant.
- Stay within insurance domain.
- FINAL CHECK: Does this response sound like a human insurance support agent working inside an app?
""";
        
        var tokenStream = _ai.StreamAsync(prompt);

        await foreach (var token in tokenStream)
        {
            await Response.WriteAsync(token);
            await Response.Body.FlushAsync();
            fullResponse.Append(token);
        }

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
