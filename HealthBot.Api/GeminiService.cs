using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HealthBot.Api;

public class GeminiService : IAIService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GeminiService(IConfiguration config)
    {
        _http = new HttpClient();
        _apiKey = config["Gemini:ApiKey"]!;
    }

    public async Task<string> AskAsync(string message)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = message }
                    }
                }
            }
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={_apiKey}"
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            )
        };

        var response = await _http.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            await File.WriteAllTextAsync("gemini_error.json", errorContent); // Log to file
            throw new HttpRequestException($"Gemini API Error: {response.StatusCode} - See gemini_error.json");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;
    }
}
