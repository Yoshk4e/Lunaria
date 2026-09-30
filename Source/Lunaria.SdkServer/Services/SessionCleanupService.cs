namespace Lunaria.SdkServer.Services;

public sealed class SessionCleanupService(SessionStore sessions, ILogger<SessionCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(300);

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

            var removed = sessions.RemoveExpired();

            if (removed > 0)
                logger.LogDebug("expired {Count} SDK sessions", removed);
        }
    }
}
