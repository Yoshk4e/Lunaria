namespace Lunaria.Game.Resources.Tables;

[GameTable("C_NPCGroupTable.json", Root = "C_NPCGroupTable")]
public record CNPCGroupTable : TableRow
{
    public ulong Id { get; init; }
    public List<uint> CareerIdList { get; init; } = [];
    public List<uint> DescIdList { get; init; } = [];
}
