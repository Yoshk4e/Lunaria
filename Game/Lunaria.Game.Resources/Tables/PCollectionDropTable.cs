namespace Lunaria.Game.Resources.Tables;

[GameTable("P_CollectionDropTable.json", Root = "P_CollectionDropTable")]
public record PCollectionDropTable : TableRow
{
    public uint Id { get; init; }
    public uint CollectionDropId { get; init; }
    public uint GroupId { get; init; }
    public uint CollectionId { get; init; }
    public uint Weight { get; init; }
}
