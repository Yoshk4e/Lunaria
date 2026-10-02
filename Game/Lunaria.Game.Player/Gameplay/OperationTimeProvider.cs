namespace Lunaria.Game.Player.Gameplay;

/// <summary>A serialized player operation and its nested commands observe one UTC timestamp.</summary>
internal sealed class OperationTimeProvider(TimeProvider source) : TimeProvider
{
    private DateTimeOffset? _operationTime;
    private int _depth;

    public override DateTimeOffset GetUtcNow() => _operationTime ?? source.GetUtcNow();
    public override TimeZoneInfo LocalTimeZone => source.LocalTimeZone;
    public override long TimestampFrequency => source.TimestampFrequency;
    public override long GetTimestamp() => source.GetTimestamp();
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        source.CreateTimer(callback, state, dueTime, period);

    public IDisposable Begin(DateTimeOffset? now = null)
    {
        if (_depth == 0) _operationTime = now ?? source.GetUtcNow();
        _depth++;
        return new Scope(this);
    }

    private sealed class Scope(OperationTimeProvider owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (--owner._depth == 0) owner._operationTime = null;
        }
    }
}
