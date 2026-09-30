namespace Lunaria.Game.Characters;

public readonly record struct CharacterGrant(int Code, ulong InstId)
{
    public bool Ok => Code == 0;
    public static CharacterGrant Rejected(int code) => new(code, InstId: 0);
}
