namespace Lunaria.Game.Resources.Tables;

[GameTable("P_FunctionalNPCTable.json", Root = "P_FunctionalNPCTable")]
public record PFunctionalNPCTable : TableRow
{
    public ulong Id { get; init; }
    public ulong MapId { get; init; }
    public string Position { get; init; } = "";
    public string Rotation { get; init; } = "";

    public uint Type { get; init; }

    public ulong TemplateId { get; init; }
    public uint NpcInfoId { get; init; }
}
