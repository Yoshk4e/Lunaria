namespace Lunaria.Game.Player.Persistence.Entities;

public sealed class RoleCharacter
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long InstId { get; set; }
    public uint CharacterId { get; set; }
    public uint Level { get; set; }
    public uint Exp { get; set; }
    public uint BreakLevel { get; set; }
    public long MotiveUniqId { get; set; }

    public Role? Role { get; set; }
}
