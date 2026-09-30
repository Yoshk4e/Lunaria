namespace Lunaria.Game.Resources.Tables;

[GameTable("P_TeamLevelTable.json", Root = "P_TeamLevelTable")]
public record PTeamLevelTable : TableRow
{
    public uint Id { get; init; }
    public uint RequireTeamExp { get; init; }
    public uint MaximumExp { get; init; }
}
