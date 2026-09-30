namespace Lunaria.Game.Resources.Tables;

[GameTable("P_ChargeAwardTable.json", Root = "P_ChargeAwardTable")]
public record PChargeAwardTable : TableRow
{
    public uint Id { get; init; }
    /// <summary>EnmChargeType: 1 month card, 2 week card, 3 currency pack, 4 shop gift.</summary>
    public uint ChargeType { get; init; }
    public uint SubId { get; init; }
    public ulong StartTime { get; init; }
    public ulong EndTime { get; init; }
    public string ChinaPrice { get; init; } = "";
    public bool IsHide { get; init; }
}

[GameTable("P_ChargeMoneyTable.json", Root = "P_ChargeMoneyTable")]
public record PChargeMoneyTable : TableRow
{
    public uint Id { get; init; }
    public uint MoneyItemId { get; init; }
    public uint BaseNum { get; init; }
    public uint FirstPresentNum { get; init; }
    public uint PresentNum { get; init; }
}

[GameTable("P_MonthCardTable.json", Root = "P_MonthCardTable")]
public record PMonthCardTable : TableRow
{
    public uint Id { get; init; }
    public uint ChargeDays { get; init; }
    public uint DaysUplimit { get; init; }
    public uint NowItemId { get; init; }
    public uint NowNum { get; init; }
    public uint DayItemId { get; init; }
    public uint DayNum { get; init; }
}
