namespace Lunaria.Game.Player.Persistence;

public sealed record RoleRow(
    long Id,
    long AccountId,
    long Slot,
    string Name,
    string SecondName,
    int Gender,
    bool Initialized
);
