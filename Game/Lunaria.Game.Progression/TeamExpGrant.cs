namespace Lunaria.Game.Progression;

public readonly record struct TeamExpGrant(
    int Code,
    uint Level,
    uint Exp,
    uint LevelsGained,
    uint WorldLevel,
    uint Dropped
)
{
    public bool Ok => Code == 0;
}
