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
        // 1. Get History (Needed for greeting check)
        var history = await _memory.GetLastMessagesAsync(request.SessionId, 3);
        bool isFirstMessage = !history.Any();

        // 2. Greeting Short-Circuit (Fix for loop)
        bool isGreeting =
            request.Message.Trim().Equals("hi", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Trim().Equals("hey", StringComparison.OrdinalIgnoreCase);

        if (isGreeting)
        {
            /*
             * We must save this interaction so that subsequent messages are NOT considered "first message".
             * Otherwise, the user will be stuck in a "first message" loop if they say "hi" again or if logic depends on it.
             */
            await _memory.AddMessageAsync(request.SessionId, "user", request.Message, "QUESTION", "Greeting");
            var greetingAnswer = "Hello! How can I help you with your health insurance today?";
            await _memory.AddMessageAsync(request.SessionId, "assistant", greetingAnswer, "ANSWER", "Greeting");

            return Ok(new
            {
                Intent = "Greeting",
                Answer = greetingAnswer
            });
        }
        
        // 2a. Ticket Status Check (Regex)
        var ticketMatch = System.Text.RegularExpressions.Regex.Match(request.Message, @"(TKT-[A-Za-z0-9]+)");
        if (ticketMatch.Success)
        {
            var tid = ticketMatch.Groups[1].Value;
            var ticket = await _ticketService.GetByTicketId(tid);
            if (ticket != null)
            {
                return Ok(new
                {
                    Intent = "TicketStatus",
                    Answer = $"I found ticket {ticket.TicketId}. Status: {ticket.Status}. Created: {DateTimeOffset.FromUnixTimeSeconds(ticket.CreatedAt).ToString("g")}.",
                    TicketId = ticket.TicketId
                });
            }
        }

        // 3. HARD OVERRIDE: Explicit Agent Request
        bool explicitAgentRequest = 
            request.Message.Contains("connect to agent", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("talk to agent", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("talk to a human", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("talk to human", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("speak to human", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("real person", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("contact support", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Contains("customer support", StringComparison.OrdinalIgnoreCase);

        if (explicitAgentRequest)
        {
            var existingTicket = await _ticketService.GetActiveTicket(request.SessionId);

            if (existingTicket != null)
            {
                return Ok(new
                {
                    Intent = "TalkToAgent",
                    Answer = $"You are already connected to a human agent. Your ticket ID is {existingTicket.TicketId}.",
                    TicketId = existingTicket.TicketId
                });
            }

            var ticketId = await _ticketService.CreateTicketAsync(request.SessionId, request.Message);
            return Ok(new
            {
                Intent = "TalkToAgent",
                Answer = $"I’ve connected you to a human agent. A support ticket has been created. Ticket ID: {ticketId}.",
                TicketId = ticketId
            });
        }



        // 2. Standard RAG / Chat Flow
        // We skip intent classification for simple flows, or use it just for context building if needed
        var hybridContext = await _hybrid.BuildContext(request.SessionId, request.Message, IntentType.PolicyInfo);
        var prompt = BuildSystemPrompt(hybridContext, request.Message, isFirstMessage);
        var answer = await _ai.GenerateAsync(prompt);

        // 3. Save & Return
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
            : "Do NOT greet. Do not repeat capability statements. Only answer the question.";

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
“Iam sorry! I’m here to help with health insurance questions. Could you please tell me what you’d like to know?”

{context}

USER QUESTION:
{userMessage}

INSTRUCTIONS:
- Prefer conversation context for follow-ups.
- Use policy only when relevant.
- Stay within insurance domain.
- If the user explicitly asks about the claim process: Explain the process first, THEN optionally say "If you want, I can connect you to a human agent."
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
