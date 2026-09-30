namespace Lunaria.Game.Resources.Tables;

[GameTable("S_PlayerIniTable.json", Root = "S_PlayerIniTable")]
public record SPlayerIniTable : TableRow
{
    public uint Id { get; init; }
    public List<uint> CharacterId { get; init; } = [];
    public List<uint> TeamInfo { get; init; } = [];
    public string ItemId { get; init; } = "";
    public uint Satiety { get; init; }
    public uint SavePoint { get; init; }
    public ulong DefaultMap { get; init; }
    public string Tod { get; init; } = "";
    public uint Weather { get; init; }
}
