using Lunaria.Common.Tracking;
namespace Lunaria.Game.Inventory;

/// <summary>Cooldowns use Unix seconds and are keyed by CD type, not item ID.</summary>
public sealed partial class ItemCooldownManager(TimeProvider? timeProvider = null) : TrackedObject
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly TrackedDictionary<uint, DateTimeOffset> __tracked_readyAt = [];
    [Tracked]
    private partial TrackedDictionary<uint, DateTimeOffset> _readyAt { get; }

    public void Load(IEnumerable<(uint CdType, long ReadyUnix)> persisted)
    {
        _readyAt.Clear();
        var now = _time.GetUtcNow();

        foreach (var (cdType, readyUnix) in persisted)
        {
            var readyAt = DateTimeOffset.FromUnixTimeSeconds(readyUnix);

            if (readyAt > now)
                _readyAt[cdType] = readyAt;
        }
        AcceptLoadedState();
    }

    public IReadOnlyList<(uint CdType, long ReadyUnix)> Active()
    {
        var now = _time.GetUtcNow();
        List<(uint, long)> active = [];

        foreach (var (cdType, readyAt) in _readyAt.ToArray())
        {
            if (readyAt <= now)
            {
                _readyAt.Remove(cdType);
                continue;
            }
            active.Add((cdType, readyAt.ToUnixTimeSeconds()));
        }

        return active;
    }

    /// <summary>Cooldown end time. UnixEpoch means ready.</summary>
    public DateTimeOffset ReadyAt(uint cdType) =>
        _readyAt.TryGetValue(cdType, out var readyAt) ? readyAt : DateTimeOffset.UnixEpoch;

    public uint Start(uint cdType, uint seconds, DateTimeOffset now)
    {
        if (seconds == 0)
            return 0;

        var readyAt = now.AddSeconds(seconds);
        var unix = readyAt.ToUnixTimeSeconds();
        readyAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (readyAt < now.AddSeconds(seconds)) readyAt = readyAt.AddSeconds(1);
        if (_readyAt.TryGetValue(cdType, out var existing) && existing >= readyAt)
            return (uint)existing.ToUnixTimeSeconds();

        _readyAt[cdType] = readyAt;

        return (uint)readyAt.ToUnixTimeSeconds();
    }

}
