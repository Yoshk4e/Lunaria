namespace Lunaria.Game.Resources.Tables;

[GameTable("P_DevelopAttributeTable.json", Root = "P_DevelopAttributeTable")]
public record PDevelopAttributeTable : TableRow
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
    public int DefCriticalDamageRes { get; init; }
    public int BloodAbsorptionEfficiency { get; init; }
    public int AtkHealAddRate { get; init; }
    public int DefHealAddRate { get; init; }
    public int AtkIgnoreTargetDefence { get; init; }
    public int AtkSlashSpec { get; init; }
    public int AtkStrikeSpec { get; init; }
    public int AtkPierceSpec { get; init; }
    public int AtkFireSpec { get; init; }
    public int AtkIceSpec { get; init; }
    public int AtkThunderSpec { get; init; }
    public int AtkGravitySpec { get; init; }
    public int AtkRadiationSpec { get; init; }
    public int AtkBurstSpec { get; init; }
    public int AtkBodyPartBreakAddRate { get; init; }
    public int AtkPursueBreakDef { get; init; }
    public int AtkPursueCatalyst { get; init; }
    public int AtkAbnElemMaster { get; init; }
}
