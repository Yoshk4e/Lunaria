namespace Lunaria.Game.Resources;

public sealed record SkillCost(IReadOnlyList<ItemGrant> Items, uint Coin)
{
    public static SkillCost Free { get; } = new([], Coin: 0);

    public bool IsFree => Items.Count == 0 && Coin == 0;
}
