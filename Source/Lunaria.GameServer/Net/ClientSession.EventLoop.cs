using System.Threading.Channels;
using Lunaria.Game.Logging;
using Lunaria.Game.Player;
using Lunaria.GameServer.Handlers;
using Lunaria.Silver;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Net;

public sealed partial class ClientSession
{
    private async Task RunEventLoopAsync(
        FrameReader reader,
        EstablishedSession established,
        ulong sessionId,
        Channel<byte[]> outbound,
        Channel<PlayerNotification> notifications,
        UdpChannel udp,
        CancellationToken shutdown
    )
    {
        var events = Channel.CreateBounded<(SessionEventKind Kind, object? Payload)>(
            new BoundedChannelOptions(options.SessionQueueDepth) { FullMode = BoundedChannelFullMode.Wait });

        var player = new Player(sessionId, assets, timeProvider);
        var codec = new SessionCodec(established.Aes);
        var handle = new PlayerHandle(notifications);
        var ctx = new NetContext(player, runtime, codec, outbound.Writer, assets, metrics);
        using var logScope = logger.BeginPlayerScope(sessionId, () => ctx.Player.Roles.Active()?.Id);

        using var readersStopped = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        // NetContext can be waiting for outbound capacity without a cancellation token.
        using var closeOutbound = shutdown.Register(() => outbound.Writer.TryComplete());
        var readerTask = ReadFramesAsync(reader, events.Writer, readersStopped.Token);
        var notifyTask = BridgeNotificationsAsync(notifications.Reader, events.Writer, readersStopped.Token);
        var udpTask = BridgeUdpAsync(udp.Inbound, events.Writer, readersStopped.Token);

        using var tickSubscription = clock.Subscribe(() => events.Writer.TryWrite((SessionEventKind.Tick, null)));

        var lastActivityTick = clock.Tick;

        var stagger = (long)(player.SessionId % PersistTicks);
        var registered = false;

        try
        {
            await outbound.Writer.WriteAsync(codec.HeartbeatFrame(), shutdown).ConfigureAwait(false);

            await foreach (var (kind, payload) in events.Reader.ReadAllAsync(shutdown).ConfigureAwait(false))
            {
                switch (kind)
                {
                    case SessionEventKind.Frame: {
                        lastActivityTick = clock.Tick;
                        var keepGoing = await OnFrameAsync(ctx, codec, (Frame)payload!).ConfigureAwait(false);

                        if (!keepGoing)
                            return; 

                        break;
                    }

                    case SessionEventKind.Udp when payload is UdpInbound inbound: {
                        lastActivityTick = clock.Tick;
                        metrics.PacketReceived();
                        var plain = codec.Aes.Decrypt(inbound.Ciphertext);

                        if (inbound.PlaintextLength is 0 || inbound.PlaintextLength > (uint)plain.Length)
                        {
                            logger.LogDebug(
                                "UDP payload length {Length} out of range for {Cipher} bytes, dropping",
                                inbound.PlaintextLength, plain.Length);
                            break;
                        }

                        ctx.EnterUdp(udp);

                        try
                        {
                            await router
                                .DispatchAsync(ctx, plain.AsSpan(start: 0, (int)inbound.PlaintextLength).ToArray())
                                .ConfigureAwait(false);
                        }
                        catch (IOException ex)
                        {
                            logger.LogDebug("UDP handler io error: {Message}", ex.Message);
                            return;
                        }
                        finally
                        {
                            ctx.ExitUdp();
                        }
                        break;
                    }

                    case SessionEventKind.Notification when payload is PlayerNotification.TakeOver:
                        try
                        {
                            await ctx.NotifyAsync(new SCAccountLogin { Result = RegistryKickCode })
                                .ConfigureAwait(false);
                        }
                        catch (IOException)
                        {
                            // The client has disconnected, so continue the takeover.
                        }
                        return;

                    case SessionEventKind.Tick: {
                        var tick = clock.Tick;

                        if (tick - lastActivityTick >= IdleLimitTicks)
                        {
                            logger.LogDebug("peer went quiet ({Ticks}s), closing", tick - lastActivityTick);
                            return;
                        }

                        if ((tick + stagger) % HeartbeatTicks == 0)
                            await outbound.Writer.WriteAsync(codec.HeartbeatFrame(), shutdown).ConfigureAwait(false);

                        if ((tick + stagger) % IncomeTicks == 0 && ctx.Player.HasActiveRole)
                        {
                            await ctx.RunCommittedAsync(
                                async () => await ctx.NotifyAsync(ctx.Player.AdvanceTime(ctx.Player.UtcNow)).ConfigureAwait(false),
                                player => roleStore.SaveAsync(player)).ConfigureAwait(false);
                        }

                        if ((tick + stagger) % PersistTicks == 0)
                            await FlushAsync(ctx.Player).ConfigureAwait(false);
                        break;
                    }
                }

                if (kind is SessionEventKind.Frame or SessionEventKind.Udp && !registered && ctx.Player.IsLoggedIn)
                {
                    var key = ctx.Player.Account.AccountKey;
                    await runtime.Sessions.RegisterAsync(key, handle).ConfigureAwait(false);
                    registered = true;
                    logger.LogInformation("player online: account {Account}, online {Online}", key, runtime.Sessions.Online);
                }
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException) when (shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            await readersStopped.CancelAsync().ConfigureAwait(false);
            events.Writer.TryComplete();
            notifications.Writer.TryComplete();
            await Task.WhenAll(readerTask, notifyTask, udpTask).ConfigureAwait(false);

            try
            {
                if (!ctx.PersistenceFaulted) await FlushAsync(ctx.Player).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "final flush failed");
            }

            if (registered)
            {
                runtime.Sessions.Unregister(ctx.Player.Account.AccountKey, handle);

                logger.LogInformation("player offline: account {Account}, online {Online}",
                    ctx.Player.Account.AccountKey, runtime.Sessions.Online);
            }

            runtime.UdpSessions.Unbind(established.NotifySessionId);

            handle.AcknowledgeTakeover();

            outbound.Writer.TryComplete();
        }
    }

    private enum SessionEventKind
    {
        Frame,
        Udp,
        Notification,
        Tick
    }
}
