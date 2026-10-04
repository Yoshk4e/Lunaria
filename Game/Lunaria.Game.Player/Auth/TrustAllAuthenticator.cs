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
        } else if (attempt.ChannelName.Length > 0 && attempt.ChannelUid.Length > 0)
        {
            // The SDK login sends no userid but a channel uid fixed per SDK account, so several accounts on one
            // device keep separate saves.
            accountKey = $"{attempt.ChannelName}:{attempt.ChannelUid}";
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
