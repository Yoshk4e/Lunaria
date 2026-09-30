namespace Lunaria.Game.Resources.Tables;

[GameTable("P_GachaRebateTable.json", Root = "P_GachaRebateTable")]
public record PGachaRebateTable : TableRow
{
    public uint Id { get; init; }
    public uint TemplateId { get; init; }
    public uint DrawCount { get; init; }
    public uint ItemId { get; init; }
    public uint ItemCount { get; init; }
}
