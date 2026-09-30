using System.Threading.Channels;
using Lunaria.GameServer;
using Lunaria.GameServer.Net;
using Xunit;

namespace Lunaria.Tests;

public sealed class SessionRegistryTests
{
    [Fact]
    public async Task ConcurrentLogins_WaitForCleanup_AndEvictEachOtherInOrder()
    {
        var registry = new SessionRegistry();
        var channels = Enumerable.Range(0, 3).Select(_ => Channel.CreateUnbounded<PlayerNotification>()).ToArray();
        var handles = channels.Select(c => new PlayerHandle(c)).ToArray();
        await registry.RegisterAsync("same-account", handles[0]);
        var first = registry.RegisterAsync("same-account", handles[1]);
        var second = registry.RegisterAsync("same-account", handles[2]);
        Assert.Equal(PlayerNotification.TakeOver, await channels[0].Reader.ReadAsync());
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Same(handles[0], registry.Get("same-account"));

        registry.Unregister("same-account", handles[0]);
        handles[0].AcknowledgeTakeover();
        var winnerTask = await Task.WhenAny(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        await winnerTask;
        var firstWinner = ReferenceEquals(winnerTask, first) ? 1 : 2;
        Assert.Equal(PlayerNotification.TakeOver, await channels[firstWinner].Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        registry.Unregister("same-account", handles[firstWinner]);
        handles[firstWinner].AcknowledgeTakeover();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        var lastWinner = firstWinner == 1 ? 2 : 1;
        Assert.Same(handles[lastWinner], registry.Get("same-account"));
        Assert.False(registry.Unregister("same-account", handles[0]));
        Assert.False(registry.Unregister("same-account", handles[firstWinner]));
        Assert.Equal(1, registry.Online);
    }
}
