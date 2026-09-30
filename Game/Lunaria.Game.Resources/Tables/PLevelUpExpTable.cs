namespace Lunaria.Game.Resources.Tables;

[GameTable("P_LevelUpExpTable.json", Root = "P_LevelUpExpTable")]
public record PLevelUpExpTable : TableRow
{
    public uint Id { get; init; }
    public uint StarRequired { get; init; }
    public uint Experience { get; init; }
}
