namespace HealthBot.Ui.Models;

public class ChatResponse
{
    public string Intent { get; set; } = "";
    public string Answer { get; set; } = "";
    public string? TicketId { get; set; }
}
