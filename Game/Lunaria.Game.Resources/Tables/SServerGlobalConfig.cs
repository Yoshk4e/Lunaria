namespace Lunaria.Game.Resources.Tables;

[GameTable("S_ServerGlobalConfig.json", Root = "S_ServerGlobalConfig")]
public record SServerGlobalConfig : TableRow
{
    public uint Id { get; init; }
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
}
