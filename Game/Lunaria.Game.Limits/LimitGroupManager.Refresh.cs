namespace Lunaria.Game.Limits;

public sealed partial class LimitGroupManager
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
        Dirty();
        return true;
    }
}
