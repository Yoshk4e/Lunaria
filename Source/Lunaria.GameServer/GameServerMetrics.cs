using System.Diagnostics.Metrics;
using Lunaria.GameServer.Net;

namespace Lunaria.GameServer;


public sealed class GameServerMetrics : IDisposable
{
    public const string MeterName = "Lunaria.GameServer";
    private readonly Histogram<double> _flushDuration;
    private readonly Counter<long> _framesDropped;

    private readonly Meter _meter = new(MeterName, "0.2.0");
    private readonly Counter<long> _packetsReceived;
    private readonly Counter<long> _packetsSent;

    public GameServerMetrics(ConnectionGate gate, SessionClock clock)
    {
        _meter.CreateObservableGauge("lunaria.sessions.active", () => gate.Sessions,
            description: "Sessions past the handshake.");

        _meter.CreateObservableGauge("lunaria.connections.active", () => gate.Connections,
            description: "Sockets held, handshaking or established.");

        _meter.CreateObservableCounter("lunaria.connections.rejected", () => gate.Rejected,
            description: "Sockets refused by admission control.");

        _meter.CreateObservableGauge("lunaria.clock.subscribers", () => clock.Subscribers,
            description: "Sessions on the shared tick.");

        _packetsReceived = _meter.CreateCounter<long>("lunaria.packets.received");
        _packetsSent = _meter.CreateCounter<long>("lunaria.packets.sent");

        _framesDropped = _meter.CreateCounter<long>("lunaria.frames.dropped",
            description: "Inbound frames dropped because a session's queue was full.");
        _flushDuration = _meter.CreateHistogram<double>("lunaria.flush.duration", "ms");
    }

    public void Dispose() => _meter.Dispose();

    public void PacketReceived() => _packetsReceived.Add(1);
    public void PacketSent() => _packetsSent.Add(1);
    public void FrameDropped() => _framesDropped.Add(1);
    public void FlushCompleted(double milliseconds) => _flushDuration.Record(milliseconds);
}
