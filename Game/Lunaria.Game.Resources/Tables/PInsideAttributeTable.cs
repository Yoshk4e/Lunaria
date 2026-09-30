namespace Lunaria.Game.Resources.Tables;

[GameTable("P_InsideAttributeTable.json", Root = "P_InsideAttributeTable")]
public record PInsideAttributeTable : TableRow
{
    public uint Id { get; init; }
    public string AttrDesc { get; init; } = "";
    public string AttrEnum { get; init; } = "";
    public bool AllowCharacterOnly { get; init; }
    public bool AllowMonsterOnly { get; init; }
    public bool AllowPermyriad { get; init; }
    public bool AllowBase { get; init; }
    public bool AllowIncrease { get; init; }
    public int Maximum { get; init; }
    public int Minimum { get; init; }
}
