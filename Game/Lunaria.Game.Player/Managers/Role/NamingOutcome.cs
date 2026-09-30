namespace Lunaria.Game.Player.Managers;

public sealed record NamingOutcome(int GenderResult, int RoleNameResult, int SecondRoleNameResult)
{
    public static NamingOutcome Ok { get; } = new(GenderResult: 0, RoleNameResult: 0, SecondRoleNameResult: 0);

    public bool AllOk => GenderResult == 0 && RoleNameResult == 0 && SecondRoleNameResult == 0;

    public static NamingOutcome Rejected(int code) => new(code, code, code);
}
