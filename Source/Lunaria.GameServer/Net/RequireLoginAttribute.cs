namespace Lunaria.GameServer.Net;

public enum LoginRequirement
{
    Account,
    ActiveRole
}

/// <summary>The router checks login before running the handler or committing gameplay state.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireLoginAttribute(LoginRequirement requirement = LoginRequirement.ActiveRole) : Attribute
{
    public LoginRequirement Requirement { get; } = requirement;

    /// <summary>Error response for a Task handler that sends its own replies.</summary>
    public Type? Reply { get; set; }

    internal bool IsSatisfied(NetContext ctx) => Requirement switch {
        LoginRequirement.Account => ctx.Player.IsLoggedIn,
        LoginRequirement.ActiveRole => ctx.Player.HasActiveRole,
        _ => throw new InvalidOperationException($"Unsupported login requirement {Requirement}")
    };
}
