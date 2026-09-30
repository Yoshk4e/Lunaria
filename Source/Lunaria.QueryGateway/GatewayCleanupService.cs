namespace Lunaria.QueryGateway;

public sealed class GatewayCleanupService(InstanceRegistry registry, ILogger<GatewayCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (true)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var removed = registry.RemoveStale();

            if (removed > 0)
                logger.LogDebug("dropped {Count} stale instances", removed);
        }
    }
}
