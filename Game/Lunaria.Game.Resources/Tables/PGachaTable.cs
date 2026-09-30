namespace Lunaria.Game.Resources.Tables;

[GameTable("P_GachaTable.json", Root = "P_GachaTable")]
public record PGachaTable : TableRow
{
    public uint Id { get; init; }
    public int Type { get; init; }
    public uint ActivityId { get; init; }
    public uint CurrencyId { get; init; }
    public uint CurrencyCount { get; init; }
    public uint SsrMaxDrawCount { get; init; }
    public uint SrMaxDrawCount { get; init; }
    public uint RebateTemplateId { get; init; }
    public uint RepeatedTemplateId { get; init; }
    public uint DailyDrawLimit { get; init; }
}
