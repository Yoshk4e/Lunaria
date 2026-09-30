namespace Lunaria.SdkServer.Services;


public sealed class QueueState(int maxPlayers, ulong timeIntervalSeconds)
{
    private long _currentIndex = -1;
    private long _processedCount;

    public (ulong Index, ulong Indexoffset, ulong WaitMinutes, ulong Interval) NextPosition()
    {
        var index = Interlocked.Increment(ref _currentIndex);
        var processed = Interlocked.Read(ref _processedCount);
        var offset = Math.Max(val1: 0L, index - processed);
        var uindex = (ulong)index;
        var uoffset = (ulong)offset;
        var waitMinutes = uindex < (ulong)maxPlayers ? 0 : timeIntervalSeconds;
        return (uindex, uoffset, waitMinutes, timeIntervalSeconds);
    }

    public (ulong Index, ulong Indexoffset, ulong WaitMinutes, ulong Interval) CurrentPosition() =>
        (0, 0, 0, timeIntervalSeconds);
}
