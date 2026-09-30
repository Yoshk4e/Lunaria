using System.Globalization;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Shop;

public sealed record ShopPurchase(uint Count, DateTimeOffset Anchor);

/// <summary>
/// Purchase periods use EnmPeriodType: 1 daily, 2 weekly, 3 monthly, 4 forever. They are separate from shared quotas.
/// </summary>
public sealed partial class ShopManager(GameData assets)
{
    private readonly SortedDictionary<uint, ShopPurchase> _bought = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, ShopPurchase> Entries => _bought;

    public int Count => _bought.Count;

    public bool IsEmpty => _bought.Count == 0;

    public void Load(IEnumerable<(uint Good, uint Count, DateTimeOffset Anchor)> persisted)
    {
        _bought.Clear();

        foreach (var (good, count, anchor) in persisted)
        {
            if (assets.Shops.IsCapped(good))
                _bought[good] = new ShopPurchase(count, anchor);
        }
        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public bool ShopExists(uint shopId) => assets.Shops.ShopExists(shopId);

    public bool NeedsSatiety(uint shopId) => assets.Shops.NeedsSatiety(shopId);

    public IReadOnlyList<ShopGood> Goods(uint shopId) => assets.Shops.Goods(shopId);

    public ShopGood? GoodInShop(uint shopId, uint goodId) => assets.Shops.GoodInShop(shopId, goodId);

    public IReadOnlyList<ShopGoodsInfo> GoodsInfo(uint shopId) =>
        Goods(shopId).Select(ToGoodsInfo).ToList();

    public static ShopGoodsInfo ToGoodsInfo(ShopGood good) => new() {
        Id = good.Id,
        MoneyId = unchecked((uint)good.MoneyType),
        MoneyNum = good.CostNum,
        PeriodType = ToPeriodType(good.LimitType),
        PeriodNum = good.LimitNum
    };

    public uint BoughtOf(ShopGood good, DateTimeOffset now)
    {
        if (!good.IsLimited)
            return 0;

        RefreshOne(good, now);
        return _bought.TryGetValue(good.Id, out var entry) ? entry.Count : 0;
    }

    public int CheckQuota(ShopGood good, uint count, DateTimeOffset now)
    {
        if (!good.IsLimited || count == 0)
            return 0;

        var current = BoughtOf(good, now);

        if ((ulong)current + count > good.LimitNum)
            return (int)EnmTextCode.EnmTextShopGoodsNotEnough;

        return 0;
    }

    public int ConsumeQuota(ShopGood good, uint count, DateTimeOffset now)
    {
        if (!good.IsLimited || count == 0)
            return 0;

        RefreshOne(good, now);

        var current = _bought.TryGetValue(good.Id, out var entry) ? entry.Count : 0;

        if ((ulong)current + count > good.LimitNum)
            return (int)EnmTextCode.EnmTextShopGoodsNotEnough;

        _bought[good.Id] = new ShopPurchase(current + count, now);
        Dirty();
        return 0;
    }

    private bool RefreshOne(ShopGood good, DateTimeOffset now)
    {
        if (!_bought.TryGetValue(good.Id, out var entry))
            return false;

        if (!HasReset(good.LimitType, entry.Anchor, now))
            return false;

        _bought[good.Id] = entry with { Count = 0, Anchor = now };
        Dirty();
        return true;
    }

    private static bool HasReset(int limitType, DateTimeOffset anchor, DateTimeOffset now) =>
        ToPeriodType(limitType) switch {
            EnmPeriodType.EnmPeriodDaily => now.UtcDateTime.Date > anchor.UtcDateTime.Date,
            EnmPeriodType.EnmPeriodWeekly => WeekOf(now) > WeekOf(anchor),
            EnmPeriodType.EnmPeriodMonthly => MonthOf(now) > MonthOf(anchor),
            _ => false
        };

    private static EnmPeriodType ToPeriodType(int limitType) => limitType switch {
        1 => EnmPeriodType.EnmPeriodDaily,
        2 => EnmPeriodType.EnmPeriodWeekly,
        3 => EnmPeriodType.EnmPeriodMonthly,
        4 => EnmPeriodType.EnmPeriodForever,
        _ => EnmPeriodType.EnmPeriodNone
    };

    private static int WeekOf(DateTimeOffset moment)
    {
        var date = moment.UtcDateTime;

        return ISOWeek.GetYear(date) * 100
               + ISOWeek.GetWeekOfYear(date);
    }

    private static int MonthOf(DateTimeOffset moment) =>
        moment.UtcDateTime.Year * 12 + moment.UtcDateTime.Month;

    private void Dirty() => IsDirty = true;
}
