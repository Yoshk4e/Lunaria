namespace Lunaria.Game.Resources.Tables;

/// <summary>Systems without a feature row retain their C_SystemIDTable default.</summary>
[GameTable("P_UnlockFeatureTable.json", Root = "P_UnlockFeatureTable")]
public record PUnlockFeatureTable : TableRow
{
    public uint Id { get; init; }
    public uint UnlockConditionId { get; init; }
    public uint ClientSystemType { get; init; }
}
