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

    [HttpDelete("{sessionId}")]
    [AllowAnonymous]
    public async Task<IActionResult> ClearHistory(string sessionId)
    {
        await _memory.ClearSessionAsync(sessionId);
        return Ok(new { Message = $"History cleared for session {sessionId}" });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        Console.WriteLine("\n========================================");
        Console.WriteLine($"[NEW REQUEST] Session: {request.SessionId} | Message: {request.Message}");
        Console.WriteLine("========================================");
        
        var startTime = DateTime.UtcNow;
        
        // OPTIMIZATION: Check for greetings FIRST (no history needed)
        bool isGreeting =
            request.Message.Trim().Equals("hi", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase) ||
            request.Message.Trim().Equals("hey", StringComparison.OrdinalIgnoreCase);

        if (isGreeting)
        {
            var greetingAnswer = "Hello! How can I help you with your health insurance today?";
            
            // Parallel save for greeting
            await _memory.AddMessageBatchAsync(
                request.SessionId,
                ("user", request.Message, "QUESTION", "Greeting", "USER", 0, ""),
                ("assistant", greetingAnswer, "ANSWER", "Greeting", "BOT", 0, "")
            );

            Console.WriteLine($"[PERF] Greeting handled in {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
            
            return Ok(new
            {
                Intent = "Greeting",
                Answer = greetingAnswer
            });
        }
        
        // OPTIMIZATION: Lazy load history only when needed
        var historyStart = DateTime.UtcNow;
        var history = await _memory.GetLastMessagesAsync(request.SessionId, 6);  // Load once with limit=6
        Console.WriteLine($"[PERF] History load took: {(DateTime.UtcNow - historyStart).TotalMilliseconds}ms");
        bool isFirstMessage = !history.Any();
        
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
                Answer = $"I've connected you to a human agent. A support ticket has been created. Ticket ID: {ticketId}.",
                TicketId = ticketId
            });
        }



        // 2. Standard RAG / Chat Flow - PASS HISTORY TO AVOID DUPLICATE LOAD
        var ragStart = DateTime.UtcNow;
        var hybridContext = await _hybrid.BuildContext(request.SessionId, request.Message, IntentType.PolicyInfo, history);
        Console.WriteLine($"[PERF] RAG BuildContext took: {(DateTime.UtcNow - ragStart).TotalMilliseconds}ms");

        // Frustration Handoff Logic (Priority over Low Confidence)
        if (hybridContext.IsFrustrated)
        {
             var existing = await _ticketService.GetActiveTicket(request.SessionId);
             var tkt = existing?.TicketId ?? await _ticketService.CreateTicketAsync(request.SessionId, "Auto-created: User frustration detected");
             
             var msg = existing != null 
                ? $"I see we're going in circles. You already have an open ticket (ID: {tkt}). I've notified the agent."
                : $"I see we're going in circles and I want to make sure you get the right help. I've created a ticket (ID: {tkt}) and connected you to a human agent.";

             return Ok(new
             {
                 Intent = "TalkToAgent",
                 Answer = msg,
                 TicketId = tkt
             });
        }

        // Low Confidence Handoff Logic
        if (hybridContext.IsLowConfidence)
        {
            var keywords = new[] { "claim", "policy", "insurance", "refund", "coverage", "hospital", "payment" };
            bool isRelevant = keywords.Any(k => request.Message.Contains(k, StringComparison.OrdinalIgnoreCase));
            
            if (isRelevant)
            {
                var existing = await _ticketService.GetActiveTicket(request.SessionId);
                var tkt = existing?.TicketId ?? await _ticketService.CreateTicketAsync(request.SessionId, "Auto-created: Low confidence policy query");
                
                var msg = existing != null
                    ? $"I couldn't find details on that. You already have a ticket open (ID: {tkt})."
                    : $"I couldn't find details on that specific policy question. I've created a ticket (ID: {tkt}) and connected you to a human agent who can help.";

                return Ok(new
                {
                    Intent = "TalkToAgent",
                    Answer = msg,
                    TicketId = tkt
                });
            }
        }

        var promptStart = DateTime.UtcNow;
        var prompt = BuildSystemPrompt(hybridContext.ContextString, request.Message, isFirstMessage);
        Console.WriteLine($"[PERF] BuildSystemPrompt took: {(DateTime.UtcNow - promptStart).TotalMilliseconds}ms");
        
        var llmStart = DateTime.UtcNow;
        var answer = await _ai.GenerateAsync(prompt);
        Console.WriteLine($"[PERF] LLM GenerateAsync took: {(DateTime.UtcNow - llmStart).TotalMilliseconds}ms");

        // 3. Save & Return (PARALLEL BATCH WRITE)
        var source = hybridContext.IsLowConfidence ? "LLM" : "VECTOR_RAG";
        var conf = hybridContext.Confidence;
        
        var saveStart = DateTime.UtcNow;
        await _memory.AddMessageBatchAsync(
            request.SessionId,
            ("user", request.Message, "QUESTION", "General", "USER", 0, ""),
            ("assistant", answer, "ANSWER", "General", source, conf, "")
        );
        Console.WriteLine($"[PERF] Save messages took: {(DateTime.UtcNow - saveStart).TotalMilliseconds}ms");
        
        Console.WriteLine($"[PERF] Total request time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
        
        return Ok(new
        {
            Intent = "General",
            Answer = answer,
            Sources = hybridContext.Sources ?? new List<string>()
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

        var prompt = BuildSystemPrompt(hybridContext.ContextString, req.Message, isFirstMessage);
        
        var tokenStream = _ai.StreamAsync(prompt);

        await foreach (var token in tokenStream)
        {
            await Response.WriteAsync(token);
            await Response.Body.FlushAsync();
            fullResponse.Append(token);
        }

        var source = hybridContext.IsLowConfidence ? "LLM" : "VECTOR_RAG";
        var conf = hybridContext.Confidence;

        await _memory.AddMessageAsync(req.SessionId, "user", req.Message, "QUESTION", "General", "USER");
        await _memory.AddMessageAsync(req.SessionId, "assistant", fullResponse.ToString(), "ANSWER", "General", source, conf);
    }

    private string BuildSystemPrompt(string context, string userMessage, bool isFirstMessage)
    {
        var greetingRule = isFirstMessage 
            ? "You may greet the user briefly." 
            : "Do NOT greet. Do not repeat capability statements. Only answer the question.";

        var identityRule = "";
        if (userMessage.Contains("who are you", StringComparison.OrdinalIgnoreCase))
        {
            identityRule = "- If asked who you are, reply EXACTLY: 'I am an AI for hospital insurance bot'.";
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
You are an AI assistant for hospital insurance.

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
- “I was developed by…”
- Any self-referential or meta explanations (unless answering 'who are you')

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

    [HttpGet("vectors")]
    [AllowAnonymous]
    public IActionResult GetVectors()
    {
        var vectors = _policyRag.GetAllVectors();
        // Return a simplified view to avoid crashing browser with 768 floats x N chunks
        var cleanView = vectors.Select((v, i) => new 
        { 
            Id = i, 
            SessionId = v.SessionId,
            Preview = v.Text.Length > 50 ? v.Text.Substring(0, 50) + "..." : v.Text, 
            VectorLength = v.Embedding.Length,
            VectorSample = v.Embedding.Take(5).ToArray() 
        });
        
        return Ok(cleanView);
    }

    [HttpPost("policy/{sessionId}")]
    [AllowAnonymous]
    public async Task<IActionResult> UploadPolicy(string sessionId, [FromBody] PolicyUploadRequest req)
    {
        await _policyRag.AddUserPolicy(sessionId, req.Text);
        return Ok(new { Message = $"Policy added for session {sessionId}" });
    }

    [HttpGet("history/{sessionId}")]
    public async Task<IActionResult> GetHistory(string sessionId)
    {
        var messages = await _memory.GetLastMessagesAsync(sessionId);
        return Ok(messages);
    }
}

public record ChatRequest(string SessionId, string Message);
public record PolicyUploadRequest(string Text);
