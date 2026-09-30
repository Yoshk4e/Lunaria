namespace Lunaria.Game.Resources.Tables;

[GameTable("P_ItemTable.json", Root = "P_ItemTable")]
public record PItemTable : TableRow
{
    public uint Id { get; init; }
    public int ShowType { get; init; }
    public int UseType { get; init; }
    public List<uint> Param { get; init; } = [];
    public bool AutoUse { get; init; }
    public int Rare { get; init; }
    public uint HoldLimit { get; init; }
    public uint UseLimit { get; init; }
}
