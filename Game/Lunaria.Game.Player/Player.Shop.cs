using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public (int Code, RewardDelivery? Delivery) BuyFromShop(
        uint shopId,
        IReadOnlyList<(uint GoodId, uint Count)> basket,
        DateTimeOffset now
    )
    {
        using var operationTime = BeginOperation(now);
        var (checkedCode, lines) = Shop.CheckBasket(shopId, basket);

        if (checkedCode != 0 || lines is null)
        {
            Log.Flag("shop {ShopId} buy refused at basket check with code {Code}", shopId, checkedCode);
            return (checkedCode, null);
        }

        foreach (var line in lines)
        {
            var quota = Shop.CheckQuota(line.Good, line.Count, now);

            if (quota != 0)
            {
                Log.Flag("shop {ShopId} buy refused, good {GoodId} quota exhausted with code {Code}", shopId, line.GoodId, quota);
                return (quota, null);
            }
        }

        var price = Shop.PriceOf(lines, Shop.NeedsSatiety(shopId));

        if (price.ExceedsBalanceLimit)
        {
            Log.Flag("shop {ShopId} buy refused, price {Currencies} exceeds the balance limit", shopId, price.Currencies);
            return ((int)EnmTextCode.EnmTextShopInvalidBoughtNum, null);
        }

        if (price.Satiety > Progress.Satiety)
        {
            Log.Flag("shop {ShopId} buy refused, satiety {Satiety} below cost {Cost}", shopId, Progress.Satiety, price.Satiety);
            return ((int)EnmTextCode.EnmTextItemNotEnough, null);
        }

        RewardDelivery? delivery = null;

        var code = Purchase([], price.Currencies, () => {
            foreach (var line in lines)
            {
                var quota = Shop.ConsumeQuota(line.Good, line.Count, now);

                if (quota != 0)
                    return quota;
            }

            if (price.Satiety > 0)
            {
                var satiety = Progress.SpendSatiety((int)price.Satiety);

                if (satiety != 0)
                    return satiety;

                Gameplay.Publish(new SatietyChanged(Progress.Satiety));
            }

            delivery = GrantRewards(price.Grants, EnmItemReason.EnmItemChangeShopBuy);
            return 0;
        });

        return code != 0 ? (code, null) : (0, delivery);
    }
}
