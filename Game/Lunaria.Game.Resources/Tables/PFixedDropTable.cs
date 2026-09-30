namespace Lunaria.Game.Resources.Tables;

[GameTable("P_FixedDropTable.json", Root = "P_FixedDropTable")]
public record PFixedDropTable : TableRow
{
    public uint Id { get; init; }
    public uint DropId { get; init; }
    public uint ItemId { get; init; }
    public uint ItemCount { get; init; }
}
