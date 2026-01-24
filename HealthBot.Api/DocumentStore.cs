namespace HealthBot.Api;

public static class DocumentStore
{
    public static Dictionary<IntentType, string> Documents = new()
    {
        {
            IntentType.ClaimProcess,
            File.ReadAllText("KNOWLEDGEBASE/claim_process.txt")
        }
    };
}