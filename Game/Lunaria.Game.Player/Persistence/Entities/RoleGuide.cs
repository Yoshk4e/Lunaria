namespace Lunaria.Game.Player.Persistence.Entities;

public sealed class RoleGuide
{
    public long RoleId { get; set; }
    public string Entries { get; set; } = "";
    public DateTime UpdatedAt { get; set; }

    public Role? Role { get; set; }
}
