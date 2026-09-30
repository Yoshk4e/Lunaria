using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Net;

public sealed class ConnectionGate(GameServerOptions options, ILogger<ConnectionGate> logger)
{
    private int _connections;
    private long _lastRejectionLogTick;
    private long _rejected;
    private int _sessions;
    public int Connections => Volatile.Read(ref _connections);

    public int Sessions => Volatile.Read(ref _sessions);

    public long Rejected => Interlocked.Read(ref _rejected);

    public bool IsFull => Sessions >= options.MaxPlayers;

    public bool TryEnter()
    {
        var ceiling = options.MaxPlayers + options.MaxPendingHandshakes;

        if (Interlocked.Increment(ref _connections) > ceiling)
        {
            Interlocked.Decrement(ref _connections);
            NoteRejection(ceiling);
            return false;
        }

        return true;
    }

    public void Exit() => Interlocked.Decrement(ref _connections);

    public bool TryPromote()
    {
        if (Interlocked.Increment(ref _sessions) > options.MaxPlayers)
        {
            Interlocked.Decrement(ref _sessions);
            NoteRejection(options.MaxPlayers);
            return false;
        }

        return true;
    }

    public void Demote() => Interlocked.Decrement(ref _sessions);

    private void NoteRejection(int ceiling)
    {
        Interlocked.Increment(ref _rejected);

        var now = Environment.TickCount64 / 1000;

        if (Interlocked.Exchange(ref _lastRejectionLogTick, now) == now)
            return;

        logger.LogWarning(
            "connection refused: at capacity ({Ceiling}), {Sessions} sessions, {Connections} sockets, {Rejected} refused so far",
            ceiling, Sessions, Connections, Rejected);
    }
}
