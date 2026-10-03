namespace Lunaria.Game.Resources;

public sealed record BreakStep(
    uint BreakLevel,
    uint MaxLevel,
    uint NeedWorldLevel,
    IReadOnlyList<ItemGrant> CostItems,
    uint CostCurrency
)
{
    /// <summary>
    /// The step out of <paramref name="breakLevel"/>. A break table row holds the cost and world level of leaving
    /// that break level, as the client shows them, while the next row holds the new level cap.
    /// </summary>
    public static BreakStep? Next(IReadOnlyList<BreakStep> ladder, uint breakLevel) =>
        ladder.FirstOrDefault(step => step.BreakLevel == breakLevel) is {} from
        && ladder.FirstOrDefault(step => step.BreakLevel == breakLevel + 1) is {} to ?
            to with { NeedWorldLevel = from.NeedWorldLevel, CostItems = from.CostItems, CostCurrency = from.CostCurrency } :
            null;
}
