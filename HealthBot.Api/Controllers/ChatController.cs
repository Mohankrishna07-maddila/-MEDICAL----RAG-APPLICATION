using Microsoft.AspNetCore.Mvc;

namespace HealthBot.Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IAIService _ai;
    private readonly IntentDetectionService _intentDetector;
    private readonly RagService _rag;
    private readonly DynamoConversationMemory _memory;

    public ChatController(
        IAIService ai,
        IntentDetectionService intentDetector,
        RagService rag,
        DynamoConversationMemory memory)
    {
        _ai = ai;
        _intentDetector = intentDetector;
        _rag = rag;
        _memory = memory;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // 1️⃣ Load conversation memory (last N messages)
        var history = await _memory.GetRecentMessagesAsync(request.SessionId);

        // 2️⃣ Detect intent
        var intent = await _intentDetector.DetectAsync(request.Message);

        // 3️⃣ Route based on intent
        string answer = intent switch
        {
            IntentType.PolicyInfo =>
                "Policy info flow (RAG next)",

            IntentType.ClaimProcess =>
                await _rag.AnswerAsync(
                    request.Message,
                    intent,
                    history
                ),

            IntentType.ClaimStatus =>
                "Claim status flow (API later)",

            IntentType.TalkToAgent =>
                "Creating support ticket...",

            _ =>
                "Sorry, I didn’t understand."
        };

        // 4️⃣ Persist conversation to DynamoDB
        await _memory.AddMessageAsync(
            request.SessionId,
            "user",
            request.Message
        );

        await _memory.AddMessageAsync(
            request.SessionId,
            "assistant",
            answer
        );

        // 5️⃣ Return response
        return Ok(new
        {
            answer,
            intent
        });
    }
}

public record ChatRequest(string SessionId, string Message);
