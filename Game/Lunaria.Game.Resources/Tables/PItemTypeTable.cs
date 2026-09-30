namespace Lunaria.Game.Resources.Tables;

[GameTable("P_ItemTypeTable.json", Root = "P_ItemTypeTable")]
public record PItemTypeTable : TableRow
{
    public uint Id { get; init; }
    public int ItemType { get; init; }
    public bool IsPrecious { get; init; }
}
