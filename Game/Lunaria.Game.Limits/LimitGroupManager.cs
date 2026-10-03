using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Limits;

public sealed record LimitCounter(uint Count, DateTimeOffset Anchor);

public sealed partial class LimitGroupManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Limits");

    private readonly TrackedSortedDictionary<uint, LimitCounter> __tracked_counts = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, LimitCounter> _counts { get; }

    public IReadOnlyDictionary<uint, LimitCounter> Entries => _counts;

    public int Count => _counts.Count;

    public bool IsEmpty => _counts.Count == 0;

    public void Load(IEnumerable<(uint Group, uint Count, DateTimeOffset Anchor)> persisted)
    {
        _counts.Clear();

        foreach (var (group, count, anchor) in persisted)
        {
            if (assets.Limits.GroupExists(group))
                _counts[group] = new LimitCounter(count, anchor);
        }
        AcceptLoadedState();
    }

    public uint CountOf(uint group, DateTimeOffset now)
    {
        if (group == 0 || !assets.Limits.GroupExists(group))
            return 0;

        RefreshOne(group, now);
        return _counts.TryGetValue(group, out var entry) ? entry.Count : 0;
    }

    public int Consume(uint group, uint count, DateTimeOffset now)
    {
        if (group == 0 || !assets.Limits.GroupExists(group))
            return 0;

        if (count == 0)
            return 0;

        RefreshOne(group, now);

        var current = _counts.TryGetValue(group, out var entry) ? entry.Count : 0;
        var cap = assets.Limits.Cap(group);

        if (cap.HasValue && (ulong)current + count > cap.Value)
        {
            Log.Stage("limit group {GroupId} consumption refused, current {Current}, requested {Count}, cap {Cap}", group, current, count, cap.Value);
            return (int)EnmTextCode.EnmTextCountGroupLimit;
        }

        // Cap unlimited-group counters without wrapping. Keep the latest clock to prevent a second daily reset.
        var anchor = entry is not null && entry.Anchor > now ? entry.Anchor : now;
        _counts[group] = new LimitCounter((uint)Math.Min((ulong)current + count, uint.MaxValue), anchor);

        return 0;
    }

    public IReadOnlyList<CmdLimitGroup> Groups(DateTimeOffset now)
    {
        Refresh(now);

        return _counts
            .Select(kv => ToCmdLimitGroup(kv.Key, kv.Value.Count))
            .ToList();
    }

    public static CmdLimitGroup ToCmdLimitGroup(uint group, uint count) => new() {
        Id = group,
        Count = count
    };

}
