namespace Lunaria.Game.Resources.Tables;

[GameTable("P_FixedAttributeTable.json", Root = "P_FixedAttributeTable")]
public record PFixedAttributeTable : TableRow
{
    public uint Id { get; init; }
    public uint TeamId { get; init; }
    public uint PermanentLiquidMax { get; init; }
    public int MoveSpeedRatio { get; init; }
    public int ShieldEffectScale { get; init; }
}
