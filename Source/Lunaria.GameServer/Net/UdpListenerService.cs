using System.Net;
using System.Net.Sockets;
using Lunaria.Silver;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Net;

public sealed class UdpListenerService(
    ILogger<UdpListenerService> logger,
    UdpSessionRegistry udpSessions
)
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);

    public async Task RunListenerAsync(int port, CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        logger.LogInformation("SilverNet UDP channel listening on port {Port} (establish channel)", port);

        var peers = new PeerTable();
        using var sweep = new PeriodicTimer(SweepInterval);

        var sender = new UdpSender(socket, logger);
        udpSessions.AttachSender((ushort)port, sender);

        try
        {
            var receiveTask = ReceiveLoopAsync(socket, peers, port, cancellationToken);
            var sweepTask = SweepLoopAsync(peers, sweep, cancellationToken);

            try
            {
                await Task.WhenAll(receiveTask, sweepTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
        finally
        {
            udpSessions.DetachSender((ushort)port);
        }
    }

    private async Task ReceiveLoopAsync(UdpClient socket, PeerTable peers, int port, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException ex)
            {
                logger.LogWarning("UDP recv failed on port {Port}: {Message}", port, ex.Message);
                continue;
            }

            try
            {
                await HandleDatagramAsync(socket, peers, port, result.Buffer, result.RemoteEndPoint)
                    .ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                logger.LogWarning("UDP send failed on port {Port}: {Message}", port, ex.Message);
            }
        }
    }

    private async Task HandleDatagramAsync(UdpClient socket, PeerTable peers, int port, byte[] bytes, IPEndPoint peer)
    {
        if (UdpDatagram.Parse(bytes) is not {} datagram)
        {
            logger.LogDebug("UDP datagram from {Peer} shorter than header, ignoring", peer);
            return;
        }

        switch (UdpTypeExtensions.FromByte(datagram.Type))
        {
            case UdpType.Connect: {
                if (ConnectRequest.Decode(datagram.Payload) is {} request)
                {
                    var previous = peers.NoteConnect(peer);

                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug(
                            "UDP <- CONNECT from {Peer} on port {Port}: sid {Sid:x16}, blob1 {Blob1}, blob2 {Blob2}, prev {Prev}",
                            peer, port, datagram.SessionId,
                            Convert.ToHexString(request.Blob1), Convert.ToHexString(request.Blob2), previous);
                } else
                {
                    peers.Touch(peer);

                    logger.LogDebug("UDP short CONNECT payload ({Length} bytes) from {Peer}",
                        datagram.Payload.Length, peer);
                }

                var response = new UdpDatagram {
                    Type = (byte)UdpType.Connect,
                    SessionId = 0,
                    LengthField = 0,
                    Payload = new ConnectResponse(0).Encode()
                };
                var wire = response.EncodeServerResponse();
                await socket.SendAsync(wire, peer).ConfigureAwait(false);

                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("UDP -> CONNECT OK to {Peer} on port {Port} ({Total} bytes)",
                        peer, port, wire.Length);
                break;
            }

            case UdpType.Ping: {
                peers.Touch(peer);
                var pong = new PongResponse(PingRequest.Decode(datagram.Payload).Timestamp);

                var response = new UdpDatagram {
                    Type = (byte)UdpType.Pong,
                    SessionId = 0,
                    LengthField = 0,
                    Payload = pong.Encode()
                };
                await socket.SendAsync(response.EncodeServerResponse(), peer).ConfigureAwait(false);

                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace("UDP PING -> PONG to {Peer} on port {Port}: ts {Ts}", peer, port,
                        Convert.ToHexString(pong.Timestamp));
                break;
            }

            case UdpType.Data: {
                peers.Touch(peer);

                if (udpSessions.TryRoute(datagram.SessionId, datagram.Payload, datagram.LengthField, peer))
                {
                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace("UDP Data {Length} bytes for session {Sid:x16} from {Peer}",
                            datagram.Payload.Length, datagram.SessionId, peer);
                } else
                {
                    logger.LogDebug("UDP Data for unknown session {Sid:x16} from {Peer}, dropping",
                        datagram.SessionId, peer);
                }
                break;
            }

            default:
                peers.Touch(peer);

                logger.LogDebug("UDP unhandled message type {Type} from {Peer} on port {Port}",
                    datagram.Type, peer, port);
                break;
        }
    }

    private async Task SweepLoopAsync(PeerTable peers, PeriodicTimer sweep, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                if (!await sweep.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var evicted = peers.Sweep();

            if (evicted > 0)
                logger.LogDebug("UDP peers expired: {Evicted}", evicted);
        }
    }
}
