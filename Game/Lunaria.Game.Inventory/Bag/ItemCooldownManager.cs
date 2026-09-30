namespace Lunaria.Game.Inventory;

/// <summary>Cooldowns use Unix seconds and are keyed by CD type, not item ID.</summary>
public sealed class ItemCooldownManager
{
    private readonly Dictionary<uint, DateTimeOffset> _readyAt = [];

    public bool IsDirty { get; private set; }

    public void Load(IEnumerable<(uint CdType, long ReadyUnix)> persisted)
    {
        _readyAt.Clear();
        var now = DateTimeOffset.UtcNow;

        foreach (var (cdType, readyUnix) in persisted)
        {
            var readyAt = DateTimeOffset.FromUnixTimeSeconds(readyUnix);

            if (readyAt > now)
                _readyAt[cdType] = readyAt;
        }
        IsDirty = false;
    }

    public IReadOnlyList<(uint CdType, long ReadyUnix)> Active()
    {
        var now = DateTimeOffset.UtcNow;
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
        Dirty();
        return (uint)readyAt.ToUnixTimeSeconds();
    }

    public void ClearDirty() => IsDirty = false;

    private void Dirty() => IsDirty = true;
}
