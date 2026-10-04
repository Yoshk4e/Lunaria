namespace Lunaria.Game.Resources.Tables;

/// <summary>
/// What a character's talent node (Methods) gives and costs. ContentType 1 raises skill group ContentParam1 to level
/// ContentParam2, 2 unlocks it at that level, and 3 adds ContentParam2 to attribute ContentParam1.
/// </summary>
[GameTable("P_TalentContentTable.json", Root = "P_TalentContentTable")]
public sealed record PTalentContentTable : TableRow
{
    public uint Id { get; init; }
    public uint CharacterId { get; init; }
    public uint NodeId { get; init; }
    public uint ContentType { get; init; }
    public uint ContentParam1 { get; init; }
    public int ContentParam2 { get; init; }
    public List<uint> ItemIdList { get; init; } = [];
    public List<uint> ItemCountList { get; init; } = [];
    public uint MoneyCount { get; init; }
}
