using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HealthBot.Api.Models;
using System.IO;

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

        var prompt = $@"You are a customer support assistant for a Health Insurance application.
You are an AI developed by MOHAN KRISHNA MADDILA.

STRICT RULES:
- You MUST NOT mention model names (Gemma, Llama, OpenAI, Google).
- ONLY IF asked who you are: Say ""I am an AI developed by MOHAN KRISHNA MADDILA for the Health Insurance App.""
- Refuse to answer general knowledge questions (e.g. history, science) unrelated to insurance.
- If the question is ""Unknown"" intent, try to be helpful ONLY if it's about insurance or the chat history.


Allowed intents:
- PolicyInfo
- ClaimProcess
- ClaimStatus
- TalkToAgent
- Unknown (Use this for general chat. Answer the user's question using the Conversation History.)

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

    public async Task<string> GenerateAsync(string prompt)
    {
        var body = new
        {
            model = "gemma3:4b",
            prompt = prompt,
            stream = false,
            options = new
            {
                num_predict = 300
            }
        };

        var response = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/generate",
            body
        );

        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return raw!.Response.Trim();
    }

    public async IAsyncEnumerable<string> StreamAsync(string prompt)
    {
        // Using gemma3:4b as per project standard, though user example used llama3
        var req = new
        {
            model = "gemma3:4b",
            prompt = prompt,
            stream = true
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate")
        {
            Content = new StringContent(JsonSerializer.Serialize(req), System.Text.Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonDocument? json = null;
            try 
            {
                json = JsonDocument.Parse(line);
            }
            catch (JsonException) { continue; }

            if (json != null && json.RootElement.TryGetProperty("response", out var token))
            {
                yield return token.GetString()!;
            }
        }
    }

    public async Task<string> ClassifyIntentAsync(string message, List<ChatMessage> history)
    {
        var historyText = string.Join("\n", history.Select(m => $"{m.Role}: {m.Content}"));
        var prompt = $@"
SYSTEM: You are an intent classifier for a health insurance bot.
Allowed intents:
- PolicyInfo (questions about coverage, benefits, claiming, rules)
- TalkToAgent (requests to speak to human, complaints)
- Unknown (greetings, general chat, unrelated topics)

HISTORY:
{historyText}

USER QUESTION:
{message}

Respond with ONE word only: the intent name.";

        var response = await GenerateAsync(prompt);
        var intent = response.Trim();

        // Simple cleanup
        if (intent.Contains("PolicyInfo")) return "PolicyInfo";
        if (intent.Contains("TalkToAgent")) return "TalkToAgent";
        return "Unknown";
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = "";
    }
}
