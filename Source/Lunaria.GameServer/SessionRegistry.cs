using System.Collections.Concurrent;
using Lunaria.GameServer.Net;

namespace Lunaria.GameServer;


public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<string, PlayerHandle> _sessions = [];

    public int Online => _sessions.Count;

    public async Task RegisterAsync(string accountKey, PlayerHandle handle)
    {
        while (!_sessions.TryAdd(accountKey, handle))
        {
            if (!_sessions.TryGetValue(accountKey, out var previous)) continue;
            if (ReferenceEquals(previous, handle)) return;
            await previous.RequestTakeoverAsync().ConfigureAwait(false);
            _sessions.TryRemove(new KeyValuePair<string, PlayerHandle>(accountKey, previous));
        }
    }

    public bool Unregister(string accountKey, PlayerHandle ours) =>
        _sessions.TryRemove(new KeyValuePair<string, PlayerHandle>(accountKey, ours));

    public PlayerHandle? Get(string accountKey) =>
        _sessions.TryGetValue(accountKey, out var handle) ? handle : null;

    public IReadOnlyList<string> ListAccounts() =>
        _sessions.Keys.Order(StringComparer.Ordinal).ToList();
}
