using System.Threading.Channels;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Resources;
using Microsoft.EntityFrameworkCore;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task Response_WaitsForCommit_AndCanRecoverWithoutDisconnectFlush()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var before = ctx.Player.Wallet.Balance(1);
        var reachedSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = ctx.RunCommittedAsync(async () => {
            ctx.Player.GrantRewards([new ItemGrant(1, 42)], EnmItemReason.EnmItemChangeNormal);
            await ctx.SendAsync(new SCItemBagGetList { Result = 0 });
        }, async player => {
            reachedSave.SetResult();
            await releaseSave.Task;
            await _store.SaveAsync(player);
        });
        await reachedSave.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { Assert.False(outbound.Reader.TryPeek(out _)); }
        finally { releaseSave.SetResult(); }
        await operation;
        Assert.True(outbound.Reader.TryRead(out _));
        var reloaded = await _store.LoadAsync(ctx.Player, 1, DateTimeOffset.UtcNow);
        Assert.Equal(before + 42, reloaded.Wallet.Balance(1));
    }

    [Fact]
    public async Task FailedCommit_EmitsNothing_AndDisablesFurtherOperations()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var before = ctx.Player.Wallet.Balance(1);
        using (var db = new GameDbContext(_options))
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_ack BEFORE UPDATE ON role_saves BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        await Assert.ThrowsAsync<IOException>(() => ctx.RunCommittedAsync(async () => {
            ctx.Player.GrantRewards([new ItemGrant(1, 42)], EnmItemReason.EnmItemChangeNormal);
            await ctx.SendAsync(new SCItemBagGetList { Result = 0 });
        }, player => _store.SaveAsync(player)));
        Assert.True(ctx.PersistenceFaulted);
        Assert.False(outbound.Reader.TryPeek(out _));
        var reloaded = await _store.LoadAsync(ctx.Player, 1, DateTimeOffset.UtcNow);
        Assert.Equal(before, reloaded.Wallet.Balance(1));
        var invoked = false;
        await Assert.ThrowsAsync<IOException>(() => ctx.RunCommittedAsync(() => {
            invoked = true;
            return Task.CompletedTask;
        }, player => _store.SaveAsync(player)));
        Assert.False(invoked);
    }

    [Fact]
    public async Task LostResponse_AfterCommit_PreservesRewardExactlyOnceOnReload()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var before = ctx.Player.Wallet.Balance(1);
        outbound.Writer.Complete();
        await Assert.ThrowsAsync<IOException>(() => ctx.RunCommittedAsync(async () => {
            ctx.Player.GrantRewards([new ItemGrant(1, 42)], EnmItemReason.EnmItemChangeNormal);
            await ctx.SendAsync(new SCItemBagGetList { Result = 0 });
        }, player => _store.SaveAsync(player)));
        Assert.False(ctx.PersistenceFaulted);
        var reloaded = await _store.LoadAsync(ctx.Player, 1, DateTimeOffset.UtcNow);
        Assert.Equal(before + 42, reloaded.Wallet.Balance(1));
        await _store.SaveAsync(reloaded);
        reloaded = await _store.LoadAsync(reloaded, 1, DateTimeOffset.UtcNow);
        Assert.Equal(before + 42, reloaded.Wallet.Balance(1));
    }

    [Fact]
    public async Task HandlerFailure_DiscardsBufferedMessages_AndLeavesDatabaseUnchanged()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var before = ctx.Player.Wallet.Balance(1);
        await Assert.ThrowsAsync<IOException>(() => ctx.RunCommittedAsync(async () => {
            ctx.Player.Wallet.Credit(1, 42);
            await ctx.SendAsync(new SCItemBagGetList { Result = 0 });
            throw new InvalidOperationException("injected handler failure");
        }, player => _store.SaveAsync(player)));
        Assert.True(ctx.PersistenceFaulted);
        Assert.False(outbound.Reader.TryPeek(out _));
        var reloaded = await _store.LoadAsync(ctx.Player, 1, DateTimeOffset.UtcNow);
        Assert.Equal(before, reloaded.Wallet.Balance(1));
    }
}
