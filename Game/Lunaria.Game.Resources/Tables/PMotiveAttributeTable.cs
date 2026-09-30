namespace Lunaria.Game.Resources.Tables;

/// <summary>AddRate multiplies base attributes in permyriad units. The client merges the other attributes.</summary>
[GameTable("P_MotiveAttributeTable.json", Root = "P_MotiveAttributeTable")]
public record PMotiveAttributeTable : TableRow
{
    public uint Id { get; init; }
    public int Maxhp { get; init; }
    public int MaxhpAddRate { get; init; }
    public int Atk { get; init; }
    public int AtkAddRate { get; init; }
    public int Def { get; init; }
    public int DefAddRate { get; init; }
    public int AtkCriticalChance { get; init; }
    public int AtkCriticalDamage { get; init; }
}
