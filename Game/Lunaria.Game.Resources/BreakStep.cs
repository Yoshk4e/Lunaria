namespace Lunaria.Game.Resources;

public sealed record BreakStep(
    uint BreakLevel,
    uint MaxLevel,
    uint NeedWorldLevel,
    IReadOnlyList<ItemGrant> CostItems,
    uint CostCurrency
);
