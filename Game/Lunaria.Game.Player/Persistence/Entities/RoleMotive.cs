namespace Lunaria.Game.Player.Persistence.Entities;

public sealed class RoleMotive
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long UniqId { get; set; }
    public uint MotiveId { get; set; }
    public uint ItemId { get; set; }
    public long ClaimTime { get; set; }
    public uint Level { get; set; }
    public uint Exp { get; set; }
    public uint RefineLevel { get; set; }
    public uint BreakLevel { get; set; }
    public bool Locked { get; set; }
    public long EquipedTarget { get; set; }

    public Role? Role { get; set; }
}
