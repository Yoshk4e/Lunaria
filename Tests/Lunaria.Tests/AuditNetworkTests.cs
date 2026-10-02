using System.Reflection;
using System.Threading.Channels;
using Lunaria.GameServer;
using Lunaria.GameServer.Net;
using Lunaria.Silver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("Category", "Audit")]
    public async Task SocketWriteFailure_DoesNotStrandSessionOnFullOutboundQueue(bool failWrites, bool withholdEof)
    {
        using var services = RouterServices();
        var runtime = services.GetRequiredService<GameServerRuntime>();
        var options = new GameServerOptions { SessionQueueDepth = 128 };
        var clock = new SessionClock(NullLogger<SessionClock>.Instance);
        var gate = new ConnectionGate(options, NullLogger<ConnectionGate>.Instance);
        var session = new ClientSession(runtime, _assets, services.GetRequiredService<Router>(),
            _store, clock, gate, _metrics, options, NullLogger<ClientSession>.Instance);
        var aes = new AesSession(new byte[16]);
        var codec = new SessionCodec(aes);
        var request = RequestPacket((uint)EClientServerCmds.CsItemBagGetList, new CSItemBagGetList());
        // Each valid request produces one login-required response. The production
        // outbound queue holds 64 frames, so these exceed its capacity after a send failure.
        var input = Enumerable.Range(0, 66).SelectMany(_ => codec.DataFrame(request)).ToArray();
        using var stream = new AuditDuplexStream(input, failWrites, withholdEof);
        using var reader = new FrameReader(stream);
        var outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) {
            FullMode = BoundedChannelFullMode.Wait
        });
        var notifications = Channel.CreateBounded<PlayerNotification>(8);
        var established = new EstablishedSession(aes, 1, 2, 0);
        var udp = runtime.UdpSessions.Bind(established.NotifySessionId, established.UdpPort);

        // Compose the same two private loops as RunSessionAsync while retaining the
        // channel so the test can clean up a blocked writer without leaving a hung task.
        var writeMethod = typeof(ClientSession).GetMethod("WriteLoopAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var eventMethod = typeof(ClientSession).GetMethod("RunEventLoopAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var stopped = new CancellationTokenSource();
        var writes = (Task)writeMethod.Invoke(session, [stream, outbound, stopped])!;
        var events = (Task)eventMethod.Invoke(session,
            [reader, established, outbound, notifications, udp, stopped.Token])!;
        bool exited;
        try
        {
            await stream.EndOfInput.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (failWrites)
            {
                await stream.WriteFailed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await writes.WaitAsync(TimeSpan.FromSeconds(5));
            }
            exited = ReferenceEquals(events, await Task.WhenAny(events, Task.Delay(TimeSpan.FromSeconds(2))));
        }
        finally
        {
            outbound.Writer.TryComplete();
            notifications.Writer.TryComplete();
            await events.WaitAsync(TimeSpan.FromSeconds(5));
            await writes;
        }

        Assert.True(exited,
            "The socket writer exited after IOException and input reached EOF, but the session remained blocked " +
            "on its full outbound queue until the test explicitly completed that queue.");
        Assert.True(stream.ReadStopped.Task.IsCompleted, "The session must await its reader before disposing the frame buffers.");
        if (!failWrites) Assert.Equal(67, stream.WriteAttempts);
    }

    private sealed class AuditDuplexStream(byte[] input, bool failWrites, bool withholdEof) : MemoryStream(input, writable: false)
    {
        public TaskCompletionSource EndOfInput { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource WriteFailed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int WriteAttempts { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                EndOfInput.TrySetResult();
                try
                {
                    if (withholdEof) await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                finally { ReadStopped.TrySetResult(); }
            }
            return count;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteAttempts++;
            if (!failWrites) return ValueTask.CompletedTask;
            WriteFailed.TrySetResult();
            return ValueTask.FromException(new IOException("Injected socket write failure"));
        }
    }
}
