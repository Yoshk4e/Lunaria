namespace Lunaria.Game.Resources;

public static class Starter
{
    public const uint CharacterLevel = 1;

    public const uint CharacterBreakLevel = 0;

    public const uint TeamLevel = 1;

    /// <summary>
    /// Only Munin's pawn ships with this client. Add characters when their client assets are available.
    /// </summary>
    public static readonly uint[] ShippedCharacters = [1001];
}
