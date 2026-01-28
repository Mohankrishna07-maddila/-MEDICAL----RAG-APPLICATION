namespace HealthBot.Api.Services;

public class RagSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    private readonly ILogger<RagSyncBackgroundService> _logger;

    public RagSyncBackgroundService(
        IServiceProvider services,
        ILogger<RagSyncBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AUTO-SYNC] Background service started. Sync interval: {Interval} minutes", _syncInterval.TotalMinutes);
        
        // Wait 30 seconds on startup to let the app initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var ragService = scope.ServiceProvider.GetRequiredService<PolicyRagService>();

                var result = await ragService.IncrementalSyncAsync();

                if (result.FilesProcessed > 0)
                {
                    Console.WriteLine($"[AUTO-SYNC] ✅ Synced {result.FilesProcessed} files, {result.ChunksAdded} chunks in {result.DurationSeconds:F2}s");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTO-SYNC] ❌ Error during auto-sync");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }
        
        _logger.LogInformation("[AUTO-SYNC] Background service stopped");
    }
}
