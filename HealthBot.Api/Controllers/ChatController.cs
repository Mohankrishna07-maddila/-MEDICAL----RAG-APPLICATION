using Microsoft.AspNetCore.Mvc;

namespace HealthBot.Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IAIService _ai;
    private readonly DynamoConversationMemory _memory;

    public ChatController(
        IAIService ai,
        DynamoConversationMemory memory)
    {
        _ai = ai;
        _memory = memory;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // 1️⃣ Load memory
        var history = await _memory.GetRecentMessagesAsync(request.SessionId);

        // 2️⃣ ONE LLM CALL (intent + answer)
        var llmResult = await _ai.AskWithIntentAsync(
            request.Message,
            history
        );

        // 3️⃣ Persist memory
        await _memory.AddMessageAsync(
            request.SessionId,
            "user",
            request.Message
        );

        await _memory.AddMessageAsync(
            request.SessionId,
            "assistant",
            llmResult.Answer
        );

        // 4️⃣ Return
        return Ok(new
        {
            intent = llmResult.Intent,
            answer = llmResult.Answer
        });
    }
}

public record ChatRequest(string SessionId, string Message);
