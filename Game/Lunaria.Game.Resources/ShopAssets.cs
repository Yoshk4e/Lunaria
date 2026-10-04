using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class ShopAssets
{
    private readonly Dictionary<uint, ShopGood[]> _goodsByGroup = [];
    private readonly Dictionary<uint, ShopGood> _goodsById = [];
    private readonly Dictionary<uint, PShopTable> _shops = [];

    public ShopAssets(
        IReadOnlyDictionary<string, PShopTable> shops,
        IReadOnlyDictionary<string, PShopGoodsTable> goods,
        IReadOnlyDictionary<string, PShopBuffTable>? buffGoods = null
    )
    {
        // Type 1 shops (cafes) list P_ShopBuffTable groups, which have no ItemNum: each purchase gives one item.
        var all = goods.Values.Select(ToGood).Concat((buffGoods ?? new Dictionary<string, PShopBuffTable>()).Values
            .Select(row => new ShopGood(row.Id, row.Group, row.ItemId, 1, row.MoneyType, row.CostNum, row.Priority,
                row.LimitNum, row.LimitType)));

        foreach (var row in shops.Values)
        {
            _shops[row.Id] = row;
        }

        foreach (var group in all.GroupBy(good => good.Group))
        {
            _goodsByGroup[group.Key] = group
                .OrderBy(good => good.Priority)
                .ThenBy(good => good.Id)
                .ToArray();
        }

        foreach (var good in _goodsByGroup.Values.SelectMany(g => g))
        {
            _goodsById[good.Id] = good;
        }

        if (_shops.Count == 0)
            throw new ResourceException("P_ShopTable.json", "p_shoptable has no rows");

        if (_goodsById.Count == 0)
            throw new ResourceException("P_ShopGoodsTable.json", "p_shopgoodstable has no rows");
    }

    public int ShopCount => _shops.Count;
    public int GoodCount => _goodsById.Count;

    public bool ShopExists(uint shopId) => _shops.ContainsKey(shopId);

    public bool IsCapped(uint goodId) =>
        _goodsById.GetValueOrDefault(goodId)?.LimitNum > 0;

    public bool NeedsSatiety(uint shopId) => _shops.GetValueOrDefault(shopId)?.NeedDecSatiety ?? false;

    public IReadOnlyList<ShopGood> Goods(uint shopId)
    {
        if (_shops.GetValueOrDefault(shopId) is not {} shop)
            return [];

        return shop.GoodsGroupArray.SelectMany(group => _goodsByGroup.GetValueOrDefault(group) ?? [])
            .ToList();
    }

    public ShopGood? GoodInShop(uint shopId, uint goodId)
    {
        if (_goodsById.GetValueOrDefault(goodId) is not {} good)
            return null;

        if (_shops.GetValueOrDefault(shopId) is not {} shop)
            return null;

        return shop.GoodsGroupArray.Contains(good.Group) ? good : null;
    }

    private static ShopGood ToGood(PShopGoodsTable row) => new(
        row.Id,
        row.Group,
        row.ItemId,
        row.ItemNum,
        row.MoneyType,
        row.CostNum,
        row.Priority,
        row.LimitNum,
        row.LimitType);
}
