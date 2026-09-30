namespace Lunaria.Game.Resources.Tables;

[GameTable("P_OutsideAttributeTable.json", Root = "P_OutsideAttributeTable")]
public record POutsideAttributeTable : TableRow
{
    public int Id { get; init; }
    public int AttrEnum { get; init; }
    public string AttrIncreaseEnum { get; init; } = "";
    public bool AllowCharacterOnly { get; init; }
    public bool AllowMonsterOnly { get; init; }
    public bool AllowPermyriad { get; init; }
    public bool AllowBase { get; init; }
    public bool AllowIncrease { get; init; }
    public int Maximum { get; init; }
    public int Minimum { get; init; }
    public int Default { get; init; }
}
