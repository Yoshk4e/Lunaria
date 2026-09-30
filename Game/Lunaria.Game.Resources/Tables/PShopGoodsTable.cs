namespace Lunaria.Game.Resources.Tables;

[GameTable("P_ShopGoodsTable.json", Root = "P_ShopGoodsTable")]
public record PShopGoodsTable : TableRow
{
    public uint Id { get; init; }
    public uint Group { get; init; }
    public uint ItemId { get; init; }
    public uint ItemNum { get; init; }
    public int MoneyType { get; init; }
    public uint CostNum { get; init; }
    public uint Priority { get; init; }
    public uint LimitNum { get; init; }
    public int LimitType { get; init; }
}
