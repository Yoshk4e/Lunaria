using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Auth;

public sealed class TrustAllAuthenticator(ILogger<TrustAllAuthenticator> logger) : IAuthenticator
{
    public Task<AccountIdentity?> AuthenticateAsync(LoginAttempt attempt)
    {
        string accountKey;

        if (attempt.Userid.Length > 0)
        {
            accountKey = attempt.Userid;
        } else if (attempt.Udid.Length > 0)
        {
            accountKey = $"device:{attempt.Udid}";
        } else
        {
            accountKey = $"anon:{Guid.CreateVersion7()}";
            logger.LogWarning("login carried no usable identity, minting ephemeral account {Key}", accountKey);
        }

        return Task.FromResult<AccountIdentity?>(new AccountIdentity(accountKey));
    }
}
