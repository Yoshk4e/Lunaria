using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;

namespace Lunaria.Game.Gacha;

/// <summary>Grant rebates at each milestone. Automatic delivery is inferred from the lack of a claim command.</summary>
public sealed partial class GachaManager : TrackedObject
{
    public IReadOnlyList<ItemGrant> ClaimRebates(uint bannerId)
    {
        if (!_banners.TryGetValue(bannerId, out var banner)
            || !_states.TryGetValue(bannerId, out var state))
            return [];

        var due = assets.Gacha.Rebates(banner.PoolId)
            .Where(milestone => milestone.Index < 32
                                && state.Total >= milestone.DrawCount
                                && (state.ClaimedMask & milestone.Bit) == 0)
            .ToList();

        if (due.Count == 0)
            return [];

        var mask = state.ClaimedMask;

        foreach (var milestone in due)
        {
            mask |= milestone.Bit;
        }
        _states[bannerId] = state with { ClaimedMask = mask };

        Log.Event("gacha banner {PoolId} released {Count} rebate milestones at total {Total}", bannerId, due.Count, state.Total);

        return due.Select(milestone => milestone.Reward).ToList();
    }
}
