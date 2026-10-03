using System.Reflection;
using Lunaria.GameServer;
using Lunaria.GameServer.Net;
using Lunaria.Silver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "AuditRound2")]
    public async Task AuditRound2_PeerEof_ReleasesSessionEvenWhenSocketWriteIsBlocked(bool blockWrites)
    {
        using var services = RouterServices();
        var runtime = services.GetRequiredService<GameServerRuntime>();
        var options = new GameServerOptions();
        var clock = new SessionClock(NullLogger<SessionClock>.Instance);
        var session = new ClientSession(runtime, _assets, services.GetRequiredService<Router>(),
            _store, clock, new ConnectionGate(options, NullLogger<ConnectionGate>.Instance),
            _metrics, options, NullLogger<ClientSession>.Instance);
        using var stream = new AuditRound2BlockedWriter(blockWrites);
        using var reader = new FrameReader(stream);
        using var shutdown = new CancellationTokenSource();
        var established = new EstablishedSession(new AesSession(new byte[16]), 71, 72, 0);
        var runMethod = typeof(ClientSession).GetMethod("RunSessionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var run = (Task)runMethod.Invoke(session, [reader, established, runtime.AllocateSessionId(), shutdown.Token])!;
        bool completed;
        try
        {
            // Model a peer that stops receiving and then half-closes its sending side.
            // Read EOF is delivered only once the real server writer has started.
            await stream.EofDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            completed = ReferenceEquals(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(1))));
        }
        finally
        {
            // Always unblock the real session before failing the assertion.
            await shutdown.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(completed,
            "Peer EOF ended the event loop, but RunSessionAsync waited indefinitely for the blocked socket writer; " +
            "only cancelling the whole server token released the session.");
    }

    private sealed class AuditRound2BlockedWriter(bool blockWrites) : MemoryStream
    {
        private readonly TaskCompletionSource _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource EofDelivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _writeStarted.Task.WaitAsync(cancellationToken);
            EofDelivered.TrySetResult();
            return 0;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _writeStarted.TrySetResult();
            if (blockWrites) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
