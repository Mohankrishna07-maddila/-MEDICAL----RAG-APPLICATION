using Amazon.DynamoDBv2.DataModel;

namespace HealthBot.Api.Models;

[DynamoDBTable("RagSyncState")]
public class SyncState
{
    [DynamoDBHashKey]
    public string PK { get; set; } = "SYNC_STATE";
    
    [DynamoDBRangeKey]
    public string SK { get; set; } = "LAST_SYNC";
    
    public long LastSyncTimestamp { get; set; }
    public int FilesProcessed { get; set; }
    public double LastSyncDuration { get; set; }
}
