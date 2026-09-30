namespace Lunaria.Game.Resources.Tables;

[GameTable("P_LevelUpTemplateTable.json", Root = "P_LevelUpTemplateTable")]
public record PLevelUpTemplateTable : TableRow
{
    public uint Id { get; init; }
    public uint TemplateId { get; init; }
    public uint Level { get; init; }
    public uint DevelopAttributeId { get; init; }
    public uint ItemCost1 { get; init; }
    public uint ItemCost2 { get; init; }
    public uint ItemCost3 { get; init; }
    public uint CurrencyCost { get; init; }
}
