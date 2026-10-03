using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// Victory loot of a battlefield: P_BattleFieldRewardTable names its reward group, and the group row with the highest
/// WorldLevelLowerLimit not above the player's world level gives the S_DropTable pack.
/// </summary>
public sealed class BattleRewardAssets
{
    private readonly Dictionary<uint, uint> _groups = [];
    private readonly Dictionary<uint, PBattleFieldRewardGroupTable[]> _tiers = [];

    public BattleRewardAssets(
        IReadOnlyDictionary<string, PBattleFieldRewardTable> fields,
        IReadOnlyDictionary<string, PBattleFieldRewardGroupTable> groups
    )
    {
        foreach (var row in fields.Values)
        {
            _groups[row.Id] = row.RewardGroupId;
        }

        foreach (var group in groups.Values.GroupBy(row => row.RewardGroupId))
        {
            _tiers[group.Key] = [.. group.OrderBy(row => row.WorldLevelLowerLimit)];
        }
    }

    /// <summary>The drop pack for a won battlefield, or 0 when it has no loot at this world level.</summary>
    public uint DropFor(uint battleFieldId, uint worldLevel)
    {
        if (!_groups.TryGetValue(battleFieldId, out var group) || !_tiers.TryGetValue(group, out var tiers))
            return 0;

        return tiers.LastOrDefault(row => row.WorldLevelLowerLimit <= worldLevel)?.DropId ?? 0;
    }
}
