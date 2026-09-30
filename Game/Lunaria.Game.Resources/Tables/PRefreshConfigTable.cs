namespace Lunaria.Game.Resources.Tables;

[GameTable("P_RefreshConfigTable.json", Root = "P_RefreshConfigTable")]
public record PRefreshConfigTable : TableRow
{
    public uint Id { get; init; }
    public int RefreshType { get; init; }
    public uint Param01 { get; init; }
    public uint Param02 { get; init; }
}
