namespace Lunaria.Game.Resources.Tables;

[GameTable("P_MotiveBreakTemplateTable.json", Root = "P_MotiveBreakTemplateTable")]
public record PMotiveBreakTemplateTable : TableRow
{
    public uint Id { get; init; }
    public uint TemplateId { get; init; }
    public uint BreakLevel { get; init; }
    public uint MaxLevel { get; init; }
    public uint NeedWorldLevel { get; init; }
    public List<uint> CostItemId { get; init; } = [];
    public List<uint> CostItemCount { get; init; } = [];
    public uint CostCurrency { get; init; }
    public uint AddAttributeId { get; init; }
}
