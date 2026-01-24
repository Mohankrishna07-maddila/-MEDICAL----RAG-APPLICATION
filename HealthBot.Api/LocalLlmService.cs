using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace HealthBot.Api;

public class LocalLlmService : IAIService
{
    private readonly HttpClient _http = new();

    public async Task<string> AskAsync(string message)
    {
        var body = new
        {
            model = "llama3",
            prompt = message,
            stream = false
        };

        var res = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/generate",
            body
        );

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.GetProperty("response").GetString()!;
    }
}
