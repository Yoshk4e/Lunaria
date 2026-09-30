namespace Lunaria.Game.Resources.Tables;

[GameTable("P_SkillGrowthTable.json", Root = "P_SkillGrowthTable")]
public record PSkillGrowthTable : TableRow
{
    public uint Id { get; init; }
    public uint InitLevel { get; init; }
    public List<uint> Skills { get; init; } = [];
    public List<uint> AffixIds { get; init; } = [];
}
