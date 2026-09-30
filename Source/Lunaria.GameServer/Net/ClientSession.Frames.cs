using System.Threading.Channels;
using Lunaria.Silver;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Net;


public sealed partial class ClientSession
{
    private async Task<bool> OnFrameAsync(NetContext ctx, SessionCodec codec, Frame frame)
    {
        Incoming incoming;

        try
        {
            incoming = Incoming.Decode(frame);
        }
        catch (SilverDecodeException ex)
        {
            logger.LogDebug("malformed session frame: {Detail}", ex.Detail);
            return false;
        }

        switch (incoming)
        {
            case Incoming.Heartbeat hb:
                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace("client heartbeat: seq {Seq}, ts {Ts}, metric {Metric}",
                        frame.Sequence, hb.TimestampMs, hb.Metric);
                return true;

            case Incoming.Data data: {
                metrics.PacketReceived();
                var plain = data.Decrypt(codec.Aes);

                try
                {
                    await router.DispatchAsync(ctx, plain).ConfigureAwait(false);
                    return true;
                }
                catch (IOException ex)
                {
                    logger.LogDebug("handler io error: {Message}", ex.Message);
                    return false;
                }
            }

            default:
                logger.LogDebug("unexpected frame in established session: seq {Seq}, type {Type:X2}",
                    frame.Sequence, frame.Type);
                return false;
        }
    }

    private async Task ReadFramesAsync(
        FrameReader reader,
        ChannelWriter<(SessionEventKind, object?)> events,
        CancellationToken shutdown
    )
    {
        while (!shutdown.IsCancellationRequested)
        {
            Frame? frame;

            try
            {
                frame = await reader.NextFrameAsync(shutdown).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                break;
            }

            if (frame is null)
                break;

            try
            {
                await events.WriteAsync((SessionEventKind.Frame, frame), shutdown).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ChannelClosedException or OperationCanceledException)
            {
                break;
            }
        }

        events.TryComplete();
    }

    private static async Task BridgeNotificationsAsync(
        ChannelReader<PlayerNotification> notifications,
        ChannelWriter<(SessionEventKind, object?)> events
    )
    {
        await foreach (var notification in notifications.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await events.WriteAsync((SessionEventKind.Notification, notification)).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                break;
            }
        }
    }


    private static async Task BridgeUdpAsync(
        ChannelReader<UdpInbound> udpInbound,
        ChannelWriter<(SessionEventKind, object?)> events
    )
    {
        await foreach (var datagram in udpInbound.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await events.WriteAsync((SessionEventKind.Udp, datagram)).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                break;
            }
        }
    }
}
