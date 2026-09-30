namespace Lunaria.Game.Resources.Tables;

[GameTable("P_GraphicGuideTable.json", Root = "P_GraphicGuideTable")]
public record PGraphicGuideTable : TableRow
{
    public uint Id { get; init; }
    public uint DropId { get; init; }
    public ulong StepId { get; init; }
}
