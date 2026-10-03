namespace Lunaria.Game.Resources.Tables;

[GameTable("P_BattleFieldRewardTable.json", Root = "P_BattleFieldRewardTable")]
public sealed record PBattleFieldRewardTable : TableRow
{
    public uint Id { get; init; }
    public uint RewardGroupId { get; init; }
}

[GameTable("P_BattleFieldRewardGroupTable.json", Root = "P_BattleFieldRewardGroupTable")]
public sealed record PBattleFieldRewardGroupTable : TableRow
{
    public uint Id { get; init; }
    public uint RewardGroupId { get; init; }
    public uint WorldLevelLowerLimit { get; init; }
    public uint DropId { get; init; }

    /// <summary>Daily cap group. Not enforced yet.</summary>
    public uint RewardLimitId { get; init; }

    public List<string> RewardShow { get; init; } = [];
}
