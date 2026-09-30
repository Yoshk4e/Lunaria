namespace Lunaria.Game.Gacha;

public enum PullKind
{
    Character,
    Motive
}

public sealed record PullOutcome(PullKind Kind, uint Id, uint Rarity, bool IsFeatured);
