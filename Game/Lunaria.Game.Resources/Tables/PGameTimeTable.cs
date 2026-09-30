namespace Lunaria.Game.Resources.Tables;

[GameTable("P_GameTimeTable.json", Root = "P_GameTimeTable")]
public sealed record PGameTimeTable : TableRow
{
    public uint Id { get; init; }
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
    public List<string> Weather { get; init; } = [];
}
