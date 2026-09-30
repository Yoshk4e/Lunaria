namespace Lunaria.Game.Resources.Tables;

[GameTable("P_ShopTable.json", Root = "P_ShopTable")]
public record PShopTable : TableRow
{
    public uint Id { get; init; }
    public int Type { get; init; }
    public List<uint> GoodsGroupArray { get; init; } = [];
    public uint ElapsedTime { get; init; }
    public bool NeedDecSatiety { get; init; }
}
