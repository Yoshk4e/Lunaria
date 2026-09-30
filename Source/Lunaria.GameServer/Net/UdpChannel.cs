using System.Net;
using System.Threading.Channels;

namespace Lunaria.GameServer.Net;

public sealed class UdpChannel
{
    private readonly Channel<UdpInbound> _inbound;
    private volatile IPEndPoint? _peer;
    private volatile UdpSender? _sender;

    internal UdpChannel(ulong sessionId, UdpSender? sender)
    {
        SessionId = sessionId;
        _sender = sender;

        _inbound = Channel.CreateBounded<UdpInbound>(new BoundedChannelOptions(64) {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public ulong SessionId { get; }

    public ChannelReader<UdpInbound> Inbound => _inbound.Reader;

    internal UdpSender? Sender
    {
        get => _sender;
        set => _sender = value;
    }

    internal void NotePeer(IPEndPoint peer) => _peer = peer;

    internal bool TryEnqueue(UdpInbound datagram) => _inbound.Writer.TryWrite(datagram);

    internal async ValueTask SendAsync(byte[] datagram)
    {
        var sender = _sender;
        var peer = _peer;

        if (sender is null || peer is null)
            return;

        await sender.SendAsync(datagram, peer).ConfigureAwait(false);
    }
}
