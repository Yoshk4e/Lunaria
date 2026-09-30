namespace Lunaria.Game.Resources.Tables;

/// <summary>
/// UnlockType 1 requires all events and 2 requires any event. The client shows a toast only with FinishDesc.
/// </summary>
[GameTable("P_UnlockConditionTable.json", Root = "P_UnlockConditionTable")]
public record PUnlockConditionTable : TableRow
{
    public uint Id { get; init; }
    public uint UnlockType { get; init; }
    public List<uint> FinishEvents { get; init; } = [];
    public uint FinishDesc { get; init; }
    public uint UnFinishDesc { get; init; }
}
