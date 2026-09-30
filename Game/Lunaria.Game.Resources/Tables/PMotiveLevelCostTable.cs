namespace Lunaria.Game.Resources.Tables;

[GameTable("P_MotiveLevelCostTable.json", Root = "P_MotiveLevelCostTable")]
public record PMotiveLevelCostTable : TableRow
{
    public uint Id { get; init; }
    public uint Rare { get; init; }
    public uint Level { get; init; }
    public uint MaxExp { get; init; }
}
