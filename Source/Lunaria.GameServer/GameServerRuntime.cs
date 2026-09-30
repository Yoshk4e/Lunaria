using Lunaria.Game.Player.Auth;
using Lunaria.Game.Player.Persistence;
using Lunaria.GameServer.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer;

public sealed class GameServerRuntime(
    IDbContextFactory<GameDbContext> db,
    IAuthenticator auth,
    ILogger<GameServerRuntime> logger
)
{
    private long _nextSessionId;

    public IDbContextFactory<GameDbContext> Db { get; } = db;

    public IAuthenticator Auth { get; } = auth;

    public SessionRegistry Sessions { get; } = new();

    public UdpSessionRegistry UdpSessions { get; } = new();

    public ILogger Logger { get; } = logger;

    public ulong AllocateSessionId() => (ulong)Interlocked.Increment(ref _nextSessionId);
}
