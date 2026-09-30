namespace Lunaria.Game.Resources;

public sealed record ShopGood(
    uint Id,
    uint Group,
    uint ItemId,
    uint ItemNum,
    int MoneyType,
    uint CostNum,
    uint Priority,
    uint LimitNum,
    int LimitType
)
{
    public bool IsLimited => LimitNum > 0;
}
