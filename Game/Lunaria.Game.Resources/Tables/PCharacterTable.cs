namespace Lunaria.Game.Resources.Tables;

[GameTable("P_CharacterTable.json", Root = "P_CharacterTable")]
public record PCharacterTable : TableRow
{
    public uint Id { get; init; }
    public int IdentityType { get; init; }
    public int ElementType { get; init; }
    public int RareType { get; init; }
    public uint FixedAttributeId { get; init; }
    public uint LevelUpTemplateId { get; init; }
    public uint BreakUpTemplateId { get; init; }
    public uint AwakenItemId { get; init; }
    public uint InitSilverCreature { get; init; }
}
