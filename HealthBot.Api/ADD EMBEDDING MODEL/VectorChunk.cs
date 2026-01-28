namespace HealthBot.Api.Models;

public class VectorChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique ID for every chunk
    public string Text { get; set; } = "";
    public string SessionId { get; set; } = "GLOBAL";
    public float[] Embedding { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new();
}
