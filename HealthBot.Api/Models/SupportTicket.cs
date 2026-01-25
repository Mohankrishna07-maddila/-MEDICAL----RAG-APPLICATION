namespace HealthBot.Api.Models;

public class SupportTicket
{
    public string TicketId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "OPEN";
    public long CreatedAt { get; set; }
}
