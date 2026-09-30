namespace Lunaria.Game.Resources.Tables;

[GameTable("P_WorldLevelTable.json", Root = "P_WorldLevelTable")]
public record PWorldLevelTable : TableRow
{
    public uint Id { get; init; }
    public uint RequireTeamLevel { get; init; }
    public uint RequestTaskId { get; init; }
    public uint MaxTeamLevel { get; init; }
    public uint MaxGemCost { get; init; }
}
