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
        var history = await _memory.GetLastMessagesAsync(request.SessionId, 3);
        bool isFirstMessage = !history.Any();

        var hybridContext = await _hybrid.BuildContext(request.SessionId, request.Message, IntentType.PolicyInfo);
        
        var prompt = BuildSystemPrompt(hybridContext, request.Message, isFirstMessage);
        
        var answer = await _ai.GenerateAsync(prompt);
        await _memory.AddMessageAsync(request.SessionId, "user", request.Message, "QUESTION", "General");
        await _memory.AddMessageAsync(request.SessionId, "assistant", answer, "ANSWER", "General");

        return Ok(new
        {
            Intent = "General",
            Answer = answer
        });
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest req)
    {
        // ... (This method will be less used by the new frontend, but we valid keep it compatible)
        Response.ContentType = "text/plain";
        var fullResponse = new System.Text.StringBuilder();

        var history = await _memory.GetLastMessagesAsync(req.SessionId, 3);
        bool isFirstMessage = !history.Any();

        var hybridContext = await _hybrid.BuildContext(req.SessionId, req.Message, IntentType.PolicyInfo);

        var prompt = BuildSystemPrompt(hybridContext, req.Message, isFirstMessage);
        
        var tokenStream = _ai.StreamAsync(prompt);

        await foreach (var token in tokenStream)
        {
            await Response.WriteAsync(token);
            await Response.Body.FlushAsync();
            fullResponse.Append(token);
        }

        await _memory.AddMessageAsync(req.SessionId, "user", req.Message, "QUESTION", "General");
        await _memory.AddMessageAsync(req.SessionId, "assistant", fullResponse.ToString(), "ANSWER", "General");
    }

    private string BuildSystemPrompt(string context, string userMessage, bool isFirstMessage)
    {
        var greetingRule = isFirstMessage 
            ? "You may greet the user briefly." 
            : "Do NOT greet. Continue the conversation naturally.";

        var identityRule = "";
        if (userMessage.Contains("who are you", StringComparison.OrdinalIgnoreCase))
        {
            identityRule = "- Answer identity briefly and do not repeat later.";
        }

        var explainRule = "";
        if (userMessage.Contains("explain", StringComparison.OrdinalIgnoreCase) || 
            userMessage.Contains("again", StringComparison.OrdinalIgnoreCase) ||
            userMessage.Contains("before", StringComparison.OrdinalIgnoreCase))
        {
             explainRule = "- Do NOT greet.\n- Do NOT ask clarifying questions.\n- Simply rephrase or simplify the last explanation.";
        }

        return $"""
SYSTEM:
You are a professional health insurance assistant.

Rules:
- {greetingRule}
{identityRule}
{explainRule}
- Answer directly and concisely.
- Be natural and human-like.
- Do not introduce yourself unless explicitly asked.
- Never repeat the same opening phrase.
- Focus on solving the user’s problem.
- Do not ask clarifying questions unless absolutely necessary.
- If unsure, give a best-effort answer and mention assumptions.

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

{context}

USER QUESTION:
{userMessage}

INSTRUCTIONS:
- Prefer conversation context for follow-ups.
- Use policy only when relevant.
- Stay within insurance domain.
- FINAL CHECK: Does this response sound like a human insurance support agent working inside an app?
""";
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
