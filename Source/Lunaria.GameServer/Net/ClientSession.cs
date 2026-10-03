using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Silver;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Net;


public sealed partial class ClientSession(
    GameServerRuntime runtime,
    GameData assets,
    Router router,
    RoleStateStore roleStore,
    SessionClock clock,
    ConnectionGate gate,
    GameServerMetrics metrics,
    GameServerOptions options,
    ILogger<ClientSession> logger,
    TimeProvider? timeProvider = null
)
{
    public const int HeartbeatTicks = 5;
    public const int PersistTicks = 30;
    public const int IncomeTicks = 30;
    public const int IdleLimitTicks = 60;
    public const int RegistryKickCode = (int)EnmTextCode.EnmTextAccLoginRepeat;

    public async Task HandleConnectionAsync(TcpClient client, CancellationToken shutdown)
    {
        var peer = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var promoted = false;

        try
        {
            client.NoDelay = true;

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("TCP connection received from {Peer}", peer);

            var localPort = (client.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0;

            var stream = client.GetStream();
            using var reader = new FrameReader(stream);

            EstablishedSession established;

            using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(shutdown))
            {
                handshakeCts.CancelAfter(TimeSpan.FromSeconds(options.HandshakeTimeoutSeconds));

                try
                {
                    established = await ServerHandshake
                        .EstablishAsync(reader, stream, (ushort)localPort, handshakeCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is SilverProtocolException or IOException or OperationCanceledException)
                {
                    logger.LogDebug("handshake failed, dropping client {Peer}: {Message}", peer, ex.Message);
                    return;
                }
            }

            if (!gate.TryPromote())
            {
                logger.LogDebug("dropping established client {Peer}: instance at capacity", peer);
                return;
            }

            promoted = true;

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("session established: notify-sid {NotifySid:x16}", established.NotifySessionId);

            await RunSessionAsync(reader, established, shutdown).ConfigureAwait(false);
        }
        finally
        {
            if (promoted)
                gate.Demote();
            gate.Exit();
            client.Close();
        }
    }

    private async Task RunSessionAsync(FrameReader reader, EstablishedSession established, CancellationToken shutdown)
    {
        var outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) {
            FullMode = BoundedChannelFullMode.Wait
        });

        var notifications = Channel.CreateBounded<PlayerNotification>(new BoundedChannelOptions(8) {
            FullMode = BoundedChannelFullMode.Wait
        });

        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var stream = reader.Stream;
        var writeTask = WriteLoopAsync(stream, outbound, stopped);

        var udp = runtime.UdpSessions.Bind(established.NotifySessionId, established.HelloSessionId, established.UdpPort);

        try
        {
            await RunEventLoopAsync(reader, established, outbound, notifications, udp, stopped.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            outbound.Writer.TryComplete();
            // Allow final notifications to drain, but bound writes to a peer that stopped reading.
            stopped.CancelAfter(TimeSpan.FromMilliseconds(250));

            try
            {
                await writeTask.ConfigureAwait(false);
            }
            catch (IOException)
            {
               
            }
        }
    }

    private async Task WriteLoopAsync(Stream writer, Channel<byte[]> frames, CancellationTokenSource stopped)
    {
        try
        {
            await foreach (var bytes in frames.Reader.ReadAllAsync(stopped.Token).ConfigureAwait(false))
            {
                await writer.WriteAsync(bytes, stopped.Token).ConfigureAwait(false);
                await writer.FlushAsync(stopped.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            logger.LogDebug("write loop stopping: {Message}", ex.Message);
            // Wake both blocked producers and the reader, even if the peer never sends EOF.
            frames.Writer.TryComplete();
            await stopped.CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested)
        {
        }
        finally
        {
            frames.Writer.TryComplete();
        }
    }
}
