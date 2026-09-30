namespace Lunaria.Game.Resources.Tables;

[GameTable("P_TmpCharacterTable.json", Root = "P_TmpCharacterTable")]
public record PTmpCharacterTable : TableRow
{
    public uint Id { get; init; }
    public uint CharacterId { get; init; }
    public uint Level { get; init; }
    /// <summary>HP in basis points, where 10000 means full.</summary>
    public uint CharacterHpRatio { get; init; }
    public uint CharacterPermanentLiquidRatio { get; init; }
    public List<uint> AffixParams { get; init; } = [];
    public List<uint> MotiveParams { get; init; } = [];
    public List<uint> TalentParams { get; init; } = [];
    public List<uint> CharacterSkillGrowth { get; init; } = [];
    public List<uint> CharacterSkillGrowthLv { get; init; } = [];
}
