namespace Lunaria.Game.Player.Auth;

public interface IAuthenticator
{
    /// <summary>Failures return an EnmTextCode value.</summary>
    Task<AccountIdentity?> AuthenticateAsync(LoginAttempt attempt);
}
