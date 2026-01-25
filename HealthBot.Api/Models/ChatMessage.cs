namespace HealthBot.Api.Models;

public class ChatMessage
{
    public string SessionId { get; set; } = default!;
    public string Role { get; set; } = default!; // user / assistant
    public string Content { get; set; } = default!;
    public long Timestamp { get; set; }
}
