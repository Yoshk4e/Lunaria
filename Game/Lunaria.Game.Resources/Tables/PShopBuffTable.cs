namespace Lunaria.Game.Resources.Tables;

/// <summary>Goods of the buff shops (P_ShopTable Type 1, such as the Voicepipe Cafe). Each purchase is one item.</summary>
[GameTable("P_ShopBuffTable.json", Root = "P_ShopBuffTable")]
public record PShopBuffTable : TableRow
{
    public uint Id { get; init; }
    public uint Group { get; init; }
    public uint ItemId { get; init; }
    public int MoneyType { get; init; }
    public uint CostNum { get; init; }
    public uint Priority { get; init; }
    public uint LimitNum { get; init; }
    public int LimitType { get; init; }
}
