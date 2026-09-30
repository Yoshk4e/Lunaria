namespace Lunaria.Game.Resources.Tables;

[GameTable("P_SkillGrowthCostTable.json", Root = "P_SkillGrowthCostTable")]
public record PSkillGrowthCostTable : TableRow
{
    public uint Id { get; init; }
    public uint GrowthId { get; init; }
    public uint Level { get; init; }
    public List<uint> CostItemId { get; init; } = [];
    public List<uint> CostItemNum { get; init; } = [];
    public uint CostCoinNum { get; init; }
}
