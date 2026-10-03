namespace Lunaria.Game.Player.Persistence.Entities;

public sealed class RoleSaveSection
{
    public long RoleId { get; set; }
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
    public Role? Role { get; set; }
}
