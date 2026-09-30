namespace Lunaria.Game.Resources.Tables;

[GameTable("P_ItemEffectTable.json", Root = "P_ItemEffectTable")]
public record PItemEffectTable : TableRow
{
    public uint Id { get; init; }
    public uint TypeId { get; init; }
    public int Satiety { get; init; }
    public List<uint> BuffList { get; init; } = [];
}
