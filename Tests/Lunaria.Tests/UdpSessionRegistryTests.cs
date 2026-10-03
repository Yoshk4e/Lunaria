using System.Net;
using Lunaria.GameServer.Net;
using Xunit;

namespace Lunaria.Tests;

public sealed class UdpSessionRegistryTests
{
    private static readonly IPEndPoint Peer = new(IPAddress.Loopback, 51295);

    [Fact]
    public void Data_RoutesByTheNotifyOrTheHelloSessionId()
    {
        var registry = new UdpSessionRegistry();
        var channel = registry.Bind(notifySessionId: 0x95ad0ea325c94391, helloSessionId: 0x02421cb1561da27b, udpPort: 30000);

        // The CBT1 client sends its position syncs with the hello session id.
        Assert.True(registry.TryRoute(0x02421cb1561da27b, [1], 1, Peer));
        Assert.True(registry.TryRoute(0x95ad0ea325c94391, [2], 1, Peer));
        Assert.False(registry.TryRoute(0xdda7448333b8ce37, [3], 1, Peer));

        Assert.True(channel.Inbound.TryRead(out var first));
        Assert.True(channel.Inbound.TryRead(out var second));
        Assert.Equal([1, 2], new[] { first!.Ciphertext[0], second!.Ciphertext[0] });
    }

    [Fact]
    public void Unbind_ReleasesBothIdsButNotAChannelThatTookTheHelloIdOver()
    {
        var registry = new UdpSessionRegistry();
        registry.Bind(notifySessionId: 1, helloSessionId: 10, udpPort: 30000);
        registry.Unbind(notifySessionId: 1, helloSessionId: 10);
        Assert.False(registry.TryRoute(1, [1], 1, Peer));
        Assert.False(registry.TryRoute(10, [1], 1, Peer));

        registry.Bind(notifySessionId: 2, helloSessionId: 20, udpPort: 30000);
        registry.Bind(notifySessionId: 3, helloSessionId: 20, udpPort: 30000);
        registry.Unbind(notifySessionId: 2, helloSessionId: 20);
        Assert.True(registry.TryRoute(20, [1], 1, Peer));
    }
}
