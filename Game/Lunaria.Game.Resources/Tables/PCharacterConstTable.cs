namespace Lunaria.Game.Resources.Tables;

/// <summary>
/// Matches CharacterData.InitCharacterConst: row 1 is currency, 2-4 are XP items, and 5-8 are awake settings.
/// </summary>
[GameTable("P_CharacterConstTable.json", Root = "P_CharacterConstTable")]
public record PCharacterConstTable : TableRow
{
    public uint Id { get; init; }

    public uint Value { get; init; }
}
