namespace Lunaria.Game.Characters;

public readonly record struct ExpGrant(
    int Code,
    uint Level,
    uint Exp,
    uint LevelsGained,
    uint Dropped
)
{
    public bool Ok => Code == 0;
    public static ExpGrant Rejected(int code) => new(code, Level: 0, Exp: 0, LevelsGained: 0, Dropped: 0);
}
