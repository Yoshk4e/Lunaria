namespace Lunaria.Game.Resources.Tables;

[GameTable("P_SubRegionTable.json", Root = "P_SubRegionTable")]
public record PSubRegionTable : TableRow
{
    public ulong Id { get; init; }
    public ulong SubRegionName { get; init; }
    public string RegionIcon { get; init; } = "";
    public string RegionBgIcon { get; init; } = "";
}
