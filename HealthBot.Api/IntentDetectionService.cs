using System.Text.Json;

namespace HealthBot.Api;

public class IntentDetectionService
{
    private readonly IAIService _ai;

    public IntentDetectionService(IAIService ai)
    {
        _ai = ai;
    }

    public async Task<IntentType> DetectAsync(string userMessage)
    {
        var prompt = $$"""
You are an intent classifier for a health insurance support system.

Classify the user message into ONE of these intents:
- PolicyInfo
- ClaimProcess
- ClaimStatus
- TalkToAgent
- Unknown

Return ONLY valid JSON like:
{ "intent": "PolicyInfo" }

User message:
"{{userMessage}}"
""";

        var response = await _ai.AskAsync(prompt);

        try
        {
            using var doc = JsonDocument.Parse(response);
            var intent = doc.RootElement
                .GetProperty("intent")
                .GetString();

            return intent switch
            {
                "PolicyInfo" => IntentType.PolicyInfo,
                "ClaimProcess" => IntentType.ClaimProcess,
                "ClaimStatus" => IntentType.ClaimStatus,
                "TalkToAgent" => IntentType.TalkToAgent,
                _ => IntentType.Unknown
            };
        }
        catch
        {
            return IntentType.Unknown;
        }
    }
}
