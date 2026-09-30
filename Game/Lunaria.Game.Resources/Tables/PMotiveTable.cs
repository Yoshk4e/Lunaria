namespace Lunaria.Game.Resources.Tables;

[GameTable("P_MotiveTable.json", Root = "P_MotiveTable")]
public record PMotiveTable : TableRow
{
    public uint Id { get; init; }
    public uint Rare { get; init; }
    public uint Identity { get; init; }
    public uint MainAttribute { get; init; }
    public uint LevelTemplateId { get; init; }
    public uint BreakTemplateId { get; init; }
}
