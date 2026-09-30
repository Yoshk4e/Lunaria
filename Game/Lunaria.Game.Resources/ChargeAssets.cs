using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>Charge goods cost bind diamonds equal to the pack's immediate currency grant.</summary>
public sealed record ChargeGood(
    PChargeAwardTable Row,
    string Currency,
    uint Price,
    IReadOnlyList<ItemGrant> Delivery,
    MonthCardDelivery? MonthCard
);

public sealed record MonthCardDelivery(PMonthCardTable Card, IReadOnlyList<ItemGrant> Immediate, IReadOnlyList<ItemGrant> Daily);

public sealed class ChargeAssets
{
    public const uint MonthCard = 1;

    public const uint MoneyShop = 3;

    public const uint PriceCurrencyItem = 770002;
    private readonly Dictionary<uint, PMonthCardTable> _cards = [];

    private readonly Dictionary<uint, PChargeAwardTable> _goods = [];
    private readonly Dictionary<uint, PChargeMoneyTable> _moneyPacks = [];

    public ChargeAssets(
        IReadOnlyDictionary<string, PChargeAwardTable> goods,
        IReadOnlyDictionary<string, PChargeMoneyTable> moneyPacks,
        IReadOnlyDictionary<string, PMonthCardTable> cards,
        ItemAssets items
    )
    {
        foreach (var row in goods.Values)
        {
            _goods[row.Id] = row;
        }

        foreach (var row in moneyPacks.Values)
        {
            _moneyPacks[row.Id] = row;
        }

        foreach (var row in cards.Values)
        {
            _cards[row.Id] = row;
        }

        if (_goods.Count == 0)
            throw new ResourceException("P_ChargeAwardTable.json", "p_chargeawardtable has no rows");

        foreach (var row in _goods.Values)
        {
            if (row.ChargeType is not (MonthCard or MoneyShop))
                continue;

            if (!items.Exists(PriceCurrencyItem))
                throw new ResourceException(
                    "P_ItemTable.json", $"price currency item {PriceCurrencyItem} is missing");

            if (row.ChargeType == MoneyShop && !_moneyPacks.ContainsKey(row.SubId))
                throw new ResourceException(
                    "P_ChargeMoneyTable.json", $"good {row.Id} references missing money pack {row.SubId}");

            if (row.ChargeType == MonthCard && !_cards.ContainsKey(row.SubId))
                throw new ResourceException(
                    "P_MonthCardTable.json", $"good {row.Id} references missing month card {row.SubId}");
        }
    }

    public IReadOnlyList<PChargeAwardTable> Offered => _goods.Values
        .Where(row => !row.IsHide && Fulfillable(row))
        .OrderBy(row => row.Id)
        .ToList();

    public ChargeGood? Offer(uint goodsId, bool firstPurchase, DateTimeOffset now)
    {
        if (!_goods.TryGetValue(goodsId, out var row)
            || row.IsHide
            || !Fulfillable(row)
            || !InWindow(row, now))
            return null;

        switch (row.ChargeType)
        {
            case MoneyShop: {
                var pack = _moneyPacks[row.SubId];
                var bonus = firstPurchase ? pack.FirstPresentNum : pack.PresentNum;

                return new ChargeGood(
                    row,
                    "GEM",
                    pack.BaseNum,
                    [new ItemGrant(pack.MoneyItemId, pack.BaseNum + bonus)],
                    MonthCard: null);
            }

            case MonthCard: {
                var card = _cards[row.SubId];

                return new ChargeGood(
                    row,
                    "GEM",
                    card.NowNum,
                    [new ItemGrant(card.NowItemId, card.NowNum)],
                    new MonthCardDelivery(
                        card,
                        [new ItemGrant(card.NowItemId, card.NowNum)],
                        [new ItemGrant(card.DayItemId, card.DayNum)]));
            }

            default:
                return null;
        }
    }

    public PMonthCardTable? MonthCardOf(uint cardId) => _cards.GetValueOrDefault(cardId);

    /// <summary>This dump has no delivery tables for shop gifts (type 4) or week cards (type 2).</summary>
    private bool Fulfillable(PChargeAwardTable row) =>
        row.ChargeType switch {
            MoneyShop => _moneyPacks.ContainsKey(row.SubId),
            MonthCard => _cards.ContainsKey(row.SubId),
            _ => false
        };

    private static bool InWindow(PChargeAwardTable row, DateTimeOffset now)
    {
        var unix = now.ToUnixTimeSeconds();

        return (row.StartTime == 0 || unix >= (long)row.StartTime)
               && (row.EndTime == 0 || unix <= (long)row.EndTime);
    }
}
