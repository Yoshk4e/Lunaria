using System.Diagnostics;
using System.Net;

namespace Lunaria.Silver;

public sealed class PeerTable
{
    private static readonly TimeSpan PeerTtl = TimeSpan.FromSeconds(300);

    private readonly Dictionary<EndPoint, Peer> _peers = [];

    public int Count => _peers.Count;

    public PeerState StateOf(EndPoint endPoint) =>
        _peers.TryGetValue(endPoint, out var peer) ? peer.State : PeerState.AwaitingConnect;

    public PeerState NoteConnect(EndPoint endPoint)
    {
        var previous = StateOf(endPoint);
        _peers[endPoint] = new Peer(PeerState.Established, Stopwatch.GetTimestamp());
        return previous;
    }

    public void Touch(EndPoint endPoint)
    {
        _peers[endPoint] = new Peer(StateOf(endPoint), Stopwatch.GetTimestamp());
    }

    public int Sweep()
    {
        var now = Stopwatch.GetTimestamp();
        var before = _peers.Count;

        foreach (var (endPoint, peer) in _peers.Where(kv =>
                         Stopwatch.GetElapsedTime(kv.Value.LastSeenTimestamp, now) >= PeerTtl)
                     .Select(kv => (kv.Key, kv.Value))
                     .ToList())
        {
            _peers.Remove(endPoint, out _);
        }
        return before - _peers.Count;
    }

    private sealed record Peer(PeerState State, long LastSeenTimestamp);
}
