namespace Lunaria.Game.Resources.Tables;

[GameTable("P_TeleportPointTemplateTable.json", Root = "P_TeleportPointTemplateTable")]
public record PTeleportPointTemplateTable : TableRow
{
    public ulong Id { get; init; }
    public uint InteractingEntityId { get; init; }
    public uint ActivateRange { get; init; }
}
