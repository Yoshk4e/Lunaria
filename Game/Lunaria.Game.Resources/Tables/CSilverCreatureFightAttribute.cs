namespace Lunaria.Game.Resources.Tables;

/// <summary>
/// CBT1 client table. The row ID is SilverCreatureID * 100000 + quality * 100 + level
/// (s_CSM_OA_SilverCreatureOutsideAttribute.GetInitAttribute); columns are attribute names.
/// </summary>
[GameTable("C_SilverCreatureFightAttribute.json", Root = "C_SilverCreatureFightAttribute")]
public record CSilverCreatureFightAttribute : TableRow
{
    public ulong Id { get; init; }
    public int Maxhp { get; init; }
    public int Atk { get; init; }
    public int Def { get; init; }
    public int AtkSlashSpec { get; init; }
    public int AtkStrikeSpec { get; init; }
    public int AtkPierceSpec { get; init; }
    public int AtkFireSpec { get; init; }
    public int AtkIceSpec { get; init; }
    public int AtkThunderSpec { get; init; }
    public int AtkGravitySpec { get; init; }
    public int AtkRadiationSpec { get; init; }
    public int AtkBurstSpec { get; init; }
    public int AtkCriticalChance { get; init; }
    public int AtkCriticalDamage { get; init; }
    public int AtkHealAddRate { get; init; }
}
