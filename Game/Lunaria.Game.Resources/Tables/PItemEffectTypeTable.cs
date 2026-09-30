namespace Lunaria.Game.Resources.Tables;

/// <summary>Client effect types: 1 HP, 2 permanent liquid, 3 temporary liquid silver.</summary>
[GameTable("P_ItemEffectTypeTable.json", Root = "P_ItemEffectTypeTable")]
public record PItemEffectTypeTable : TableRow
{
    public uint Id { get; init; }
    public int Revive { get; init; }
    public int EffectType { get; init; }
    public int Cd { get; init; }
}
