namespace Lunaria.Game.Resources.Tables;

[GameTable("C_SubRegionData.json", Root = "C_SubRegionData")]
public record CSubRegionData : TableRow
{
    public ulong Id { get; init; }
    public ulong NameId { get; init; }
    public string WorldLocation { get; init; } = "";
    public uint RedBarrageGroupId { get; init; }
    public uint WhiteBarrageGroupId { get; init; }
}
