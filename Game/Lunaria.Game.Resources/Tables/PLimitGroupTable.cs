namespace Lunaria.Game.Resources.Tables;

[GameTable("P_LimitGroupTable.json", Root = "P_LimitGroupTable")]
public record PLimitGroupTable : TableRow
{
    public uint Id { get; init; }
    public uint RewardLimit { get; init; }
    public uint RewardLimitRefreshConfigId { get; init; }
}
