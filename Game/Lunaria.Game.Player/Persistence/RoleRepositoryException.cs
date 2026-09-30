namespace Lunaria.Game.Player.Persistence;

public sealed class RoleRepositoryException(RoleRepositoryError error) : Exception(error switch {
    RoleRepositoryError.NameTaken => "role name already taken",
    RoleRepositoryError.CapReached => "role cap reached (3 per account)",
    _ => "role persistence failed"
})
{
    public RoleRepositoryError Error { get; } = error;
}
