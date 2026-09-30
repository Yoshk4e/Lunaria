namespace Lunaria.Game.Resources.Tables;

[GameTable("P_CharacterSkillGroupTable.json", Root = "P_CharacterSkillGroupTable")]
public record PCharacterSkillGroupTable : TableRow
{
    public uint Id { get; init; }
    public List<uint> SkillGroup { get; init; } = [];
}
