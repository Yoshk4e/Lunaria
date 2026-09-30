using Google.Protobuf;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.GameServer.Handlers.Recv;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.DependencyInjection;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VitalsOnlyChange_SurvivesCommitAndReload(bool liquid)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var id = ctx.Player.Characters.All.First().InstId;
        Assert.False(ctx.Player.IsDirty);
        if (liquid) Assert.Equal(0, ctx.Player.Characters.SetPermanentLiquid(id, 0));
        else Assert.Equal(0, ctx.Player.Characters.SetHp(id, 0));
        await _store.SaveAsync(ctx.Player);
        Assert.False(ctx.Player.IsDirty);
        var reloaded = await _store.LoadAsync(ctx.Player, 1, DateTimeOffset.UtcNow);
        Assert.Equal(0, liquid ? reloaded.Characters.PermanentLiquid(id) : reloaded.Characters.Hp(id));
        if (!liquid)
        {
            await _store.SaveAsync(reloaded);
            reloaded.HealRoster();
            await _store.SaveAsync(reloaded);
            reloaded = await _store.LoadAsync(reloaded, 1, DateTimeOffset.UtcNow);
            Assert.Equal(reloaded.Characters.MaxHp(id), reloaded.Characters.Hp(id));
        }
    }

    [Fact]
    public async Task ExistingTemporaryTeam_WithMissingTeamData_ReturnsErrorWithoutFaulting()
    {
        using var services = RouterServices();
        var router = services.GetRequiredService<Router>();
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var table = _assets.TmpTeams.GetBySrc(1)!;
        Assert.NotNull(table);
        var request = new CSCharacterUpdateTmpTeam { TeamType = (int)table.TaskType, TeamSrc = table.Id };
        var response = await new HandleCharacterUpdateTmpTeam().OnPacket(ctx, request);
        Assert.NotEqual(0, response.Result);
        Assert.Empty(ctx.Player.TempTeams.Teams);
        await router.DispatchAsync(ctx, RequestPacket((uint)EClientServerCmds.CsCharacterUpdateTmpTeam, request));
        Assert.False(ctx.PersistenceFaulted);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(93)]
    public async Task RewardSpendClaimAndReload_ConservesEveryItemAndCoin(int seed)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        const uint item = 21206001;
        player.Bag.Add(item, _assets.Items.HoldLimit(item));
        long expectedItems = CountOwned(player);
        var expectedCoins = player.Wallet.Balance(1);
        var random = new Random(seed);
        var overflows = 0;
        for (var step = 0; step < 160; step++)
        {
            var count = (uint)random.Next(1, 70);
            switch (random.Next(4))
            {
                case 0:
                    var reward = player.GrantRewards([new ItemGrant(item, count), new ItemGrant(1, count)],
                        EnmItemReason.EnmItemChangeNormal);
                    expectedItems += count;
                    expectedCoins += count;
                    if (reward.Mailed.Count + reward.Deferred.Count > 0) overflows++;
                    break;
                case 1:
                    var beforeItems = player.Bag.CountOf(item);
                    if (player.Bag.Remove(item, count) == 0) expectedItems -= count;
                    else Assert.Equal(beforeItems, player.Bag.CountOf(item));
                    if (player.Wallet.Debit(1, count) == 0) expectedCoins -= count;
                    break;
                case 2:
                    player.ClaimAllMailAttachments();
                    player.Mails.DeleteAllRead();
                    break;
                case 3:
                    await _store.SaveAsync(player);
                    player = await _store.LoadAsync(player, 1, DateTimeOffset.UtcNow);
                    break;
            }
            Assert.Equal(expectedItems, CountOwned(player));
            Assert.Equal(expectedCoins, player.Wallet.Balance(1));
            player.DrainGameplayChanges();
        }
        Assert.True(overflows > 0);
        await _store.SaveAsync(player);
        var otherRole = await _store.LoadAsync(player, 2, DateTimeOffset.UtcNow);
        var returned = await _store.LoadAsync(otherRole, 1, DateTimeOffset.UtcNow);
        Assert.Equal(expectedItems, CountOwned(returned));
        Assert.Equal(expectedCoins, returned.Wallet.Balance(1));

        static long CountOwned(Player p) => p.Bag.CountOf(item)
            + p.Mails.Entries.SelectMany(m => m.Items).Where(g => g.ItemId == item).Sum(g => (long)g.Count)
            + p.PendingRewardMail.SelectMany(g => g).Where(g => g.ItemId == item).Sum(g => (long)g.Count);
    }
}
