using System.Collections.Concurrent;
using System.Net;

namespace Lunaria.GameServer.Net;

public sealed class UdpSessionRegistry
{
    private readonly ConcurrentDictionary<ulong, UdpChannel> _channels = [];
    private readonly ConcurrentDictionary<ushort, UdpSender> _senders = [];

    public void AttachSender(ushort port, UdpSender sender) => _senders[port] = sender;

    public void DetachSender(ushort port) => _senders.TryRemove(port, out _);

    public UdpChannel Bind(ulong notifySessionId, ushort udpPort)
    {
        _senders.TryGetValue(udpPort, out var sender);
        var channel = new UdpChannel(notifySessionId, sender);
        _channels[notifySessionId] = channel;
        return channel;
    }

    public void Unbind(ulong notifySessionId) => _channels.TryRemove(notifySessionId, out _);

    public bool TryRoute(ulong notifySessionId, byte[] ciphertext, uint plaintextLength, IPEndPoint peer)
    {
        if (!_channels.TryGetValue(notifySessionId, out var channel))
            return false;

        channel.NotePeer(peer);
        return channel.TryEnqueue(new UdpInbound(ciphertext, plaintextLength, peer));
    }
}
