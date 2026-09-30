namespace Lunaria.Game.Resources.Tables;

[GameTable("C_SystemIDTable.json", Root = "C_SystemIDTable")]
public record CSystemIDTable : TableRow
{
    public uint Id { get; init; }
    public uint Category { get; init; }
    public uint SystemFunctionType { get; init; }
    public string SubModuleName { get; init; } = "";
    public uint DefaultUnlock { get; init; }
    public bool DefaultEnable { get; init; }
    public uint ShowCursor { get; init; }
}
