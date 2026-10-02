using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Shop;

public sealed partial class ShopManager
{

    public (int Code, IReadOnlyList<BasketLine>? Lines) CheckBasket(
        uint shopId,
        IEnumerable<(uint GoodId, uint Count)> basket
    )
    {
        if (!ShopExists(shopId))
        {
            Log.Flag("shop basket refused, shop {ShopId} does not exist", shopId);
            return ((int)EnmTextCode.EnmTextShopNotExsit, null);
        }

        var wants = new SortedDictionary<uint, ulong>();

        foreach (var (goodId, count) in basket)
        {
            if (goodId == 0 || count == 0)
                return ((int)EnmTextCode.EnmTextShopInvalidBoughtNum, null);

            wants[goodId] = wants.GetValueOrDefault(goodId) + count;
        }

        if (wants.Count == 0)
            return ((int)EnmTextCode.EnmTextShopInvalidBoughtNum, null);

        var lines = new List<BasketLine>(wants.Count);

        foreach (var (goodId, count) in wants)
        {
            if (GoodInShop(shopId, goodId) is not {} good)
            {
                Log.Flag("shop basket refused, good {GoodId} is not stocked in shop {ShopId}", goodId, shopId);
                return ((int)EnmTextCode.EnmTextShopGoodsNotExsit, null);
            }

            if (!assets.Items.IsMoneyType(good.MoneyType))
            {
                Log.Flag("shop basket refused, good {GoodId} uses unknown money type {MoneyType}", goodId, good.MoneyType);
                return ((int)EnmTextCode.EnmTextShopGoodsPayTypeError, null);
            }

            if (!assets.Items.Exists(good.ItemId))
                return ((int)EnmTextCode.EnmTextShopGoodsNotExsit, null);

            if (count > uint.MaxValue)
                return ((int)EnmTextCode.EnmTextShopInvalidBoughtNum, null);

            if (good.ItemNum * count > uint.MaxValue)
                return ((int)EnmTextCode.EnmTextShopInvalidBoughtNum, null);

            lines.Add(new BasketLine(goodId, (uint)count, good));
        }

        return (0, lines);
    }

    public BasketPrice PriceOf(IReadOnlyList<BasketLine> lines, bool needsSatiety)
    {
        var currencies = new SortedDictionary<int, UInt128>();
        var grants = new List<ItemGrant>(lines.Count);
        UInt128 satiety = 0;

        foreach (var line in lines)
        {
            UInt128 units = line.Count;

            currencies[line.Good.MoneyType] =
                currencies.GetValueOrDefault(line.Good.MoneyType) + line.Good.CostNum * units;
            grants.Add(new ItemGrant(line.Good.ItemId, line.Good.ItemNum * line.Count));
            satiety += units;
        }

        var price = currencies
            .Select(pair => (pair.Key, pair.Value > (UInt128)long.MaxValue ? long.MaxValue : (long)pair.Value))
            .ToList();
        var satietyCost = needsSatiety ? satiety > (UInt128)long.MaxValue ? long.MaxValue : (long)satiety : 0;
        return new BasketPrice(price, grants, satietyCost,
            currencies.Values.Any(v => v > (UInt128)long.MaxValue) || needsSatiety && satiety > (UInt128)long.MaxValue);
    }

    public sealed record BasketLine(uint GoodId, uint Count, ShopGood Good);

    public sealed record BasketPrice(
        IReadOnlyList<(int MoneyType, long Amount)> Currencies,
        IReadOnlyList<ItemGrant> Grants,
        long Satiety,
        bool ExceedsBalanceLimit = false
    );
}
