namespace Lunaria.Game.Resources.Tables;

[GameTable("P_SavePointTemplateTable.json", Root = "P_SavePointTemplateTable")]
public record PSavePointTemplateTable : TableRow
{
    public ulong Id { get; init; }
    public uint InteractingEntityId { get; init; }
    public bool BDefault { get; init; }
    public uint ActivateRange { get; init; }
    public uint LoginGameEffectId { get; init; }
    public string LocationOffset { get; init; } = "";
    public float LoginGameEffectLength { get; init; }
}
