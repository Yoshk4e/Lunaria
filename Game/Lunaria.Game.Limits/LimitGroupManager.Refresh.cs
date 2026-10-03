using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;

namespace Lunaria.Game.Limits;

public sealed partial class LimitGroupManager : TrackedObject
{
    public int Refresh(DateTimeOffset now)
    {
        var reset = 0;

        foreach (var group in _counts.Keys.ToList())
        {
            if (RefreshOne(group, now))
                reset++;
        }

        return reset;
    }

    private bool RefreshOne(uint group, DateTimeOffset now)
    {
        if (!_counts.TryGetValue(group, out var entry))
            return false;

        var period = assets.Limits.Period(group);

        if (!period.ResetsEver)
            return false;

        if (!period.HasReset(entry.Anchor, now))
            return false;

        _counts[group] = entry with { Count = 0, Anchor = now };

        Log.Event("limit group {GroupId} reset from count {Count}, anchor {PreviousAnchor} to {Anchor}", group, entry.Count, entry.Anchor, now);
        return true;
    }
}
