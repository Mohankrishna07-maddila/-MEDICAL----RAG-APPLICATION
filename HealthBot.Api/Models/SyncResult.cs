namespace HealthBot.Api.Models;

public class SyncResult
{
    public int FilesProcessed { get; set; }
    public int ChunksAdded { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime SyncTimestamp { get; set; }
    public List<string> ProcessedFiles { get; set; } = new();
}
