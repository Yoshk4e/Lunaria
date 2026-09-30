namespace Lunaria.Game.Resources.Tables;

[GameTable("P_BreakTemplateTable.json", Root = "P_BreakTemplateTable")]
public record PBreakTemplateTable : TableRow
{
    public uint Id { get; init; }
    public uint TemplateId { get; init; }
    public uint BreakLevel { get; init; }
    public uint MaxLevel { get; init; }
    public uint NeedWorldLevel { get; init; }
    public List<uint> CostItemId { get; init; } = [];
    public List<uint> CostItemCount { get; init; } = [];
    public uint CostCurrency { get; init; }
    public List<uint> AwardItemId { get; init; } = [];
    public List<uint> AwardItemCount { get; init; } = [];
    public uint DevelopAttributeId { get; init; }
}
