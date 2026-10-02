using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Net;

public sealed class SessionClock(ILogger<SessionClock> logger, TimeProvider? timeProvider = null) : IHostedService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<long, Action> _subscribers = [];
    private Task? _loop;
    private long _nextId;
    private CancellationTokenSource? _shutdown;
    private long _tick;

    public long Tick => Interlocked.Read(ref _tick);

    public int Subscribers => _subscribers.Count;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_shutdown.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_shutdown is null)
            return;

        await _shutdown.CancelAsync().ConfigureAwait(false);

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public IDisposable Subscribe(Action onTick)
    {
        var id = Interlocked.Increment(ref _nextId);
        _subscribers[id] = onTick;
        return new Subscription(this, id);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider ?? TimeProvider.System);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            Interlocked.Increment(ref _tick);

            foreach (var subscriber in _subscribers)
            {
                try
                {
                    subscriber.Value();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "session clock subscriber {Id} threw, dropping it", subscriber.Key);
                    _subscribers.TryRemove(subscriber.Key, out _);
                }
            }
        }
    }

    private sealed class Subscription(SessionClock clock, long id) : IDisposable
    {
        public void Dispose() => clock._subscribers.TryRemove(id, out _);
    }
}
