namespace Lunaria.Game.Resources.Tables;

[GameTable("P_TeamExpAwardTable.json", Root = "P_TeamExpAwardTable")]
public record PTeamExpAwardTable : TableRow
{
    public uint Id { get; init; }
    public uint Reason { get; init; }
    public uint TeamExp { get; init; }
}
