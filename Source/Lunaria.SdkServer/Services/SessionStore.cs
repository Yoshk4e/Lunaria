using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Lunaria.SdkServer.Services;


public sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = [];

    public (string HeiToken, string ChannelToken, string SdkUid, string ChannelUid) CreateSession(
        Guid userId,
        string email,
        long ttlSeconds
    )
    {
        var heiToken = RandomToken(33);
        var channelToken = RandomToken(129);
        var sdkUid = RandomToken(21);
        // Stable per account: the game server keys its accounts by channel uid, the client sends no userid.
        var channelUid = userId.ToString("N");

        _sessions[heiToken] = new SessionEntry(
            userId,
            email,
            sdkUid,
            channelUid,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ttlSeconds);

        return (heiToken, channelToken, sdkUid, channelUid);
    }

    public SessionEntry? Get(string heiToken) =>
        _sessions.TryGetValue(heiToken, out var entry) ? entry : null;

    public int RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expired = _sessions.Where(kv => kv.Value.ExpiresAtUnixSeconds < now).Select(kv => kv.Key).ToList();

        foreach (var key in expired)
        {
            _sessions.TryRemove(key, out _);
        }
        return expired.Count;
    }

    private static string RandomToken(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes);
    }

    public sealed record SessionEntry(
        Guid UserId,
        string Email,
        string SdkUid,
        string ChannelUid,
        long ExpiresAtUnixSeconds
    );
}
