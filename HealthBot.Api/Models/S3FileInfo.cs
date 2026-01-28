namespace HealthBot.Api.Models;

public class S3FileInfo
{
    public string Key { get; set; } = "";
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
}
