namespace Lunaria.Game.Resources.Tables;

[GameTable("P_TalentNodeTable.json", Root = "P_TalentNodeTable")]
public record PTalentNodeTable : TableRow
{
    public uint Id { get; init; }
    public uint GroupType { get; init; }
    public List<uint> ParentIdList { get; init; } = [];
    public uint UnlockLevel { get; init; }
}
