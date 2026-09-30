using System.Threading.Channels;
using Google.Protobuf;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.Proto;
using Lunaria.Silver;
using Msg;

namespace Lunaria.GameServer.Net;

public sealed class NetContext
{
    private readonly SessionCodec _codec;
    private readonly GameServerMetrics _metrics;
    private readonly ChannelWriter<byte[]> _outbound;
    private UdpChannel? _udpChannel;
    private List<byte[]>? _pendingMessages;

    public bool PersistenceFaulted { get; private set; }

    public async Task RunCommittedAsync(Func<Task> operation, Func<Player, Task> save)
    {
        if (PersistenceFaulted) throw new IOException("session has an uncommitted failed operation");
        if (_pendingMessages is not null) throw new InvalidOperationException("nested gameplay operation");
        var pending = new List<byte[]>();
        _pendingMessages = pending;
        try
        {
            await operation().ConfigureAwait(false);
            await FlushGameplayChangesAsync().ConfigureAwait(false);
            await save(Player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PersistenceFaulted = true;
            throw new IOException("gameplay operation failed before acknowledgement, reconnect to reload committed state", ex);
        }
        finally
        {
            _pendingMessages = null;
        }

        // A send failure must not undo or repeat the committed operation.
        foreach (var body in pending)
            await WriteBodyAsync(body).ConfigureAwait(false);
    }

    public NetContext(
        Player player,
        GameServerRuntime runtime,
        SessionCodec codec,
        ChannelWriter<byte[]> outbound,
        GameData assets,
        GameServerMetrics metrics
    )
    {
        Player = player;
        Runtime = runtime;
        _codec = codec;
        _outbound = outbound;
        Assets = assets;
        _metrics = metrics;
    }

    public Player Player { get; private set; }

    internal void ReplacePlayer(Player player) => Player = player;
    public GameData Assets { get; }
    public GameServerRuntime Runtime { get; }
    internal void EnterUdp(UdpChannel channel) => _udpChannel = channel;

    internal void ExitUdp() => _udpChannel = null;

    public ValueTask SendAsync<T>(T message) where T : IMessage<T> => WriteAppAsync(message);

    public ValueTask NotifyAsync<T>(T message) where T : IMessage<T> => WriteAppAsync(message);

    public ValueTask NotifyAsync(IMessage message) => WriteAppAsync(message);

    public async ValueTask NotifyAsync(IEnumerable<IMessage> messages)
    {
        await FlushGameplayChangesAsync().ConfigureAwait(false);
        foreach (var message in messages)
            await WriteMessageAsync(message).ConfigureAwait(false);
    }

    private async ValueTask WriteAppAsync(IMessage message)
    {
        await FlushGameplayChangesAsync().ConfigureAwait(false);
        await WriteMessageAsync(message).ConfigureAwait(false);
    }

    public async ValueTask FlushGameplayChangesAsync()
    {
        foreach (var change in Player.DrainGameplayChanges())
        {
            await WriteMessageAsync(change).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteMessageAsync(IMessage message)
    {
        var cmdId = MessageCmdRegistry.CmdIdOf(message)
                    ?? throw new InvalidOperationException(
                        $"{message.GetType().Name} has no [Cmd] binding, add the partial-class tag under Generated/Cmds/");

        var pkg = new CSMsgPkg {
            Head = new CSMsgHead { Version = 1, Cmd = cmdId, GameId = 1 },
            Body = message.ToByteString()
        };
        var body = pkg.ToByteArray();
        if (body.Length > ushort.MaxValue / 16 * 16)
            throw new IOException("application response exceeds the encrypted frame limit");

        if (_pendingMessages is {} pending)
        {
            pending.Add(body);
            return;
        }

        await WriteBodyAsync(body).ConfigureAwait(false);
    }

    private async ValueTask WriteBodyAsync(byte[] body)
    {

        if (_udpChannel is {} udp)
        {
            var datagram = UdpDataMessage.Encode(_codec.Aes, udp.SessionId, body);
            await udp.SendAsync(datagram).ConfigureAwait(false);
            _metrics.PacketSent();
            return;
        }

        var frame = _codec.DataFrame(body);

        try
        {
            await _outbound.WriteAsync(frame).ConfigureAwait(false);
            _metrics.PacketSent();
        }
        catch (ChannelClosedException)
        {
            throw new IOException("session outbound channel closed");
        }
    }
}
