using Lunaria.Game.Player.Persistence;

namespace Lunaria.Game.Player.Managers;

public sealed record RoleState
{
    public long Id { get; init; }
    public long Slot { get; init; }
    public string Name { get; init; } = "";
    public string SecondName { get; init; } = "";
    public int Gender { get; init; }

    public bool Initialized { get; init; }

    public static RoleState FromRow(RoleRow row) => new() {
        Id = row.Id,
        Slot = row.Slot,
        Name = row.Name,
        SecondName = row.SecondName,
        Gender = row.Gender,
        Initialized = row.Initialized
    };
}
