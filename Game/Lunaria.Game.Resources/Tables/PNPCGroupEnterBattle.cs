namespace Lunaria.Game.Resources.Tables;

[GameTable("P_NPCGroupEnterBattle.json", Root = "P_NPCGroupEnterBattle")]
public record PNPCGroupEnterBattle : TableRow
{
    public ulong Id { get; init; }
    public ulong NpcGroupId { get; init; }
    public bool Expand { get; init; }
    public List<string> EnterBattleList { get; init; } = [];
}
