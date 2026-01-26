using System.Text;
using System.Text.Json;

public class EmbeddingService
{
    private readonly HttpClient _http = new();

    public async Task<float[]> EmbedAsync(string text)
    {
        var req = new
        {
            model = "nomic-embed-text",
            prompt = text
        };

        var res = await _http.PostAsync(
            "http://localhost:11434/api/embeddings",
            new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json")
        );

        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }
}
