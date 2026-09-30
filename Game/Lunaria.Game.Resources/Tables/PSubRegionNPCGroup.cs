namespace Lunaria.Game.Resources.Tables;

[GameTable("P_SubRegionNPCGroup.json", Root = "P_SubRegionNPCGroup")]
public record PSubRegionNPCGroup : TableRow
{
    public ulong Id { get; init; }
    public List<ulong> NpcGroupIdList { get; init; } = [];
}
