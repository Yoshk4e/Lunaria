using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private readonly HashSet<uint> _boughtMoneyPacks = [];

    public IReadOnlyCollection<uint> BoughtMoneyPacks => _boughtMoneyPacks;

    public ChargeOrderOutcome BuyChargeGoods(uint goodsId)
    {
        using var operationTime = BeginOperation();
        var now = UtcNow;
        var offer = assets.Charge.Offer(goodsId, !_boughtMoneyPacks.Contains(goodsId), now);

        if (offer is null)
            return new ChargeOrderOutcome((int)EnmTextCode.EnmTextSdkGoodsNotFound, goodsId, "", Price: 0, Delivery: null);

        if (offer.MonthCard is {} renewal)
        {
            var error = MonthCards.CanBuy(renewal.Card.Id, now);
            if (error != 0) return new ChargeOrderOutcome(error, goodsId, offer.Currency, offer.Price, Delivery: null);
        }

        RewardDelivery? delivery = null;

        var code = Purchase(
            [],
            [((int)MoneyType.BindDiamond, offer.Price)],
            () => {
                if (offer.MonthCard is {} card)
                {
                    var purchase = MonthCards.Buy(card.Card.Id, now);
                    if (purchase.Result != 0) return purchase.Result;
                    foreach (var notification in DeliverMonthCardReward(card.Card.Id, purchase.Accrued))
                        _changes.Add(notification);
                    delivery = GrantRewards(purchase.Immediate, EnmItemReason.EnmItemChangeMonthcardCharge);
                } else
                {
                    delivery = GrantRewards(offer.Delivery, EnmItemReason.EnmItemChangeMonthcardCharge);
                    _boughtMoneyPacks.Add(goodsId);
                }

                return 0;
            });

        return code != 0 ?
            new ChargeOrderOutcome(code, goodsId, offer.Currency, offer.Price, Delivery: null) :
            new ChargeOrderOutcome(Code: 0, goodsId, offer.Currency, offer.Price, delivery);
    }

    public bool IsFirstChargeOf(uint goodsId) => !_boughtMoneyPacks.Contains(goodsId);

    public void LoadChargePurchases(IEnumerable<uint> boughtMoneyPacks)
    {
        using var operationTime = BeginOperation();
        _boughtMoneyPacks.Clear();

        foreach (var goodsId in boughtMoneyPacks)
        {
            _boughtMoneyPacks.Add(goodsId);
        }
    }

    public readonly record struct ChargeOrderOutcome(
        int Code,
        uint GoodsId,
        string Currency,
        uint Price,
        RewardDelivery? Delivery
    );
}
