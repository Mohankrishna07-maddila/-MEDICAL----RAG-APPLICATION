using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace HealthBot.Api;

public class LocalLlmService : IAIService
{
    private readonly HttpClient _http = new();

    public async Task<Models.LlmChatResult> AskWithIntentAsync(
        string question,
        List<ChatMessage> history)
    {
        var historyText = string.Join("\n",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var prompt = $@"You are a customer support assistant for a health insurance app.

Allowed intents:
- PolicyInfo
- ClaimProcess
- ClaimStatus
- TalkToAgent
- Unknown

Conversation history:
{historyText}

User question:
{question}

Respond ONLY in valid JSON:
{{
  ""intent"": ""..."",
  ""answer"": ""...""
}}";

        var body = new
        {
            model = "gemma3:4b",
            prompt = prompt,
            stream = false,
            options = new
            {
                num_predict = 200
            }
        };

        var response = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/generate",
            body
        );

        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<OllamaResponse>();

        var json = raw!.Response.Trim();

        // 🧹 Strip Markdown if present
        if (json.StartsWith("```"))
        {
            var lines = json.Split('\n');
            // Remove first and last lines (the backticks)
            json = string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
        }

        return JsonSerializer.Deserialize<Models.LlmChatResult>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        )!;
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = "";
    }
}
