namespace Lunaria.Game.Resources.Tables;

/// <summary>TemplateId is the motive's LevelTemplateId, not its item ID.</summary>
[GameTable("P_MotiveLevelTemplateTable.json", Root = "P_MotiveLevelTemplateTable")]
public record PMotiveLevelTemplateTable : TableRow
{
    public uint Id { get; init; }
    public uint TemplateId { get; init; }
    public uint Level { get; init; }
    public uint AddAttributeId { get; init; }
}
