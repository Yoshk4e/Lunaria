using System.Collections.Concurrent;
using System.Net;

namespace Lunaria.GameServer.Net;

public sealed class UdpSessionRegistry
{
    private readonly ConcurrentDictionary<ulong, UdpChannel> _channels = [];
    private readonly ConcurrentDictionary<ushort, UdpSender> _senders = [];

    public void AttachSender(ushort port, UdpSender sender) => _senders[port] = sender;

    public void DetachSender(ushort port) => _senders.TryRemove(port, out _);

    /// <summary>
    /// The CBT1 client stamps its UDP data (position syncs) with the hello session id, not the notify session id,
    /// so the channel answers to both.
    /// </summary>
    public UdpChannel Bind(ulong notifySessionId, ulong helloSessionId, ushort udpPort)
    {
        _senders.TryGetValue(udpPort, out var sender);
        var channel = new UdpChannel(notifySessionId, sender);
        _channels[notifySessionId] = channel;
        if (helloSessionId != notifySessionId) _channels[helloSessionId] = channel;
        return channel;
    }

    public void Unbind(ulong notifySessionId, ulong helloSessionId)
    {
        if (_channels.TryRemove(notifySessionId, out var channel) && helloSessionId != notifySessionId)
            _channels.TryRemove(new KeyValuePair<ulong, UdpChannel>(helloSessionId, channel));
    }

    public bool TryRoute(ulong notifySessionId, byte[] ciphertext, uint plaintextLength, IPEndPoint peer)
    {
        if (!_channels.TryGetValue(notifySessionId, out var channel))
            return false;

        channel.NotePeer(peer);
        return channel.TryEnqueue(new UdpInbound(ciphertext, plaintextLength, peer));
    }
}
