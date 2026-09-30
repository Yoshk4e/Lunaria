namespace Lunaria.Game.Player.Persistence.Entities;

public sealed class Role
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public long Slot { get; set; }
    public string Name { get; set; } = "";
    public string SecondName { get; set; } = "";
    public int Gender { get; set; }
    public bool Initialized { get; set; }

    /// <summary>Highest allocated character ID, or 0. Restore it at login to prevent ID reuse.</summary>
    public long LastMintedInstId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Account? Account { get; set; }
}
