namespace Lunaria.Game.Resources.Tables;

[GameTable("P_MoneyTable.json", Root = "P_MoneyTable")]
public record PMoneyTable : TableRow
{
    public uint Id { get; init; }
    public int MoneyType { get; init; }
}
