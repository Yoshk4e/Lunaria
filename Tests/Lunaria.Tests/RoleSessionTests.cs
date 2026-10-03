using System.Threading.Channels;
using Lunaria.Game.Mail;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Entities;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.GameServer;
using Lunaria.GameServer.Handlers.Recv;
using Lunaria.GameServer.Net;
using Lunaria.GameServer.Services;
using Lunaria.Silver;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed partial class RoleSessionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<GameDbContext> _options;
    private readonly GameServerMetrics _metrics;
    private readonly RoleStateStore _store;
    private readonly RoleSessionService _sessions;
    private readonly GameData _assets;
    private readonly BundledGameplayFixture _fixture;

    public RoleSessionTests(BundledGameplayFixture fixture)
    {
        _fixture = fixture;
        _assets = fixture.Data;
        _connection.Open();
        _options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(_connection).Options;
        using var db = new GameDbContext(_options);
        db.Database.Migrate();
        db.Accounts.Add(new Account { Id = 1, AccountKey = "role-test", Userid = "role-test" });
        db.Roles.AddRange(new Role { Id = 1, AccountId = 1, Slot = 0 }, new Role { Id = 2, AccountId = 1, Slot = 1 });
        db.SaveChanges();
        var factory = new TestFactory(_options);
        _store = new RoleStateStore(factory,
            new RoleRepository(factory, NullLogger<RoleRepository>.Instance),
            new CharacterRepository(factory, NullLogger<CharacterRepository>.Instance),
            new MotiveRepository(factory, NullLogger<MotiveRepository>.Instance),
            new GuideRepository(factory, NullLogger<GuideRepository>.Instance),
            new MailRepository(factory, NullLogger<MailRepository>.Instance),
            new RoleSaveRepository(factory, NullLogger<RoleSaveRepository>.Instance));
        _sessions = new RoleSessionService(_store, NullLogger<RoleSessionService>.Instance);
        _metrics = new GameServerMetrics(new ConnectionGate(new GameServerOptions(), NullLogger<ConnectionGate>.Instance),
            new SessionClock(NullLogger<SessionClock>.Instance));
    }

    private NetContext Context(Channel<byte[]>? outbound = null)
    {
        var player = new Player(1, _assets);
        player.Account.Bind(1, "role-test", "role-test", DateTimeOffset.UtcNow);
        player.Roles.Load([new RoleRow(1, 1, 0, "", "", 0, false), new RoleRow(2, 1, 1, "", "", 0, false)]);
        return new NetContext(player, null!, new SessionCodec(new AesSession(new byte[16])),
            (outbound ?? Channel.CreateUnbounded<byte[]>()).Writer, _assets, _metrics);
    }

    [Fact]
    public async Task Switch_SavesPreviousRole_AndLoadsIsolatedState()
    {
        var ctx = Context();
        var login = new HandleRoleLogin(_sessions);
        Assert.Equal(0, (await login.OnPacket(ctx, new CSRoleLogin { RoleId = 1 })).Result);
        var first = ctx.Player;
        var initialCoins = first.Wallet.Balance(1);
        var initialPotions = first.Bag.CountOf(21206001);
        first.Wallet.Credit(1, 123456);
        first.Bag.Add(21206001, 7);
        first.GrantRewards([new ItemGrant(12031001, 2)], EnmItemReason.EnmItemChangeNormal);
        var ids = first.Motives.All.Select(m => m.UniqId).ToArray();
        var bag = await new HandleItemBagGetList(NullLogger<HandleItemBagGetList>.Instance).OnPacket(ctx, new CSItemBagGetList());
        Assert.Equal(ids, bag.Items.Where(i => i.MotiveData is not null).Select(i => i.MotiveData.MotiveUniqId));
        Assert.NotEqual(0, (await new CharacterTeamMutations.UpdateTeam().OnPacket(ctx, new CSCharacterUpdateTeam())).Result);

        Assert.Equal(0, (await login.OnPacket(ctx, new CSRoleLogin { RoleId = 2 })).Result);
        Assert.NotSame(first, ctx.Player);
        Assert.Equal(initialCoins, ctx.Player.Wallet.Balance(1));
        Assert.Equal(initialPotions, ctx.Player.Bag.CountOf(21206001));
        Assert.Empty(ctx.Player.Motives.All);
        Assert.Equal(first.SessionId, ctx.Player.SessionId);
        Assert.Equal(first.Account.AccountKey, ctx.Player.Account.AccountKey);

        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(initialCoins + 123456, ctx.Player.Wallet.Balance(1));
        Assert.Equal(initialPotions + 7, ctx.Player.Bag.CountOf(21206001));
        Assert.Equal(ids, ctx.Player.Motives.All.Select(m => m.UniqId));
        Assert.True(ctx.Player.Guid.Next() > ids.Max());
    }

    [Fact]
    public async Task CharacterSelection_PreviewsWithoutActivatingOrLoadingRoleState()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        var expected = ctx.Player.Characters.ListData();
        Assert.Equal(0, await _sessions.LogoutAsync(ctx, 2));
        var session = ctx.Player;
        var response = await new HandleCharacterList(_sessions).OnPacket(ctx, new CSCharacterListReq());
        Assert.Equal(0, response.Result);
        Assert.Equal(expected, response.List);
        Assert.Same(session, ctx.Player);
        Assert.Empty(session.Characters.All);
        Assert.False(session.HasActiveRole);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public async Task SavedEmptyInventory_DoesNotReceiveStarterGrantsAgain()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        foreach (var item in ctx.Player.Bag.ItemsData()) ctx.Player.Bag.Remove(item.ItemId, item.ItemNum);
        var balance = ctx.Player.Wallet.Balance(1);
        Assert.Equal(0, ctx.Player.Wallet.Debit(1, balance));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Empty(ctx.Player.Bag.ItemsData());
        Assert.Equal(0, ctx.Player.Wallet.Balance(1));
    }

    [Theory]
    [InlineData("role")]
    [InlineData("guide")]
    [InlineData("mail")]
    public async Task CorruptDestination_LeavesCurrentRoleInstalled(string part)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var current = ctx.Player;
        using (var db = new GameDbContext(_options))
        {
            if (part == "role") db.RoleSaves.Add(new RoleSave { RoleId = 2, State = "{" });
            if (part == "guide") db.RoleGuides.Add(new RoleGuide { RoleId = 2, Entries = "{" });
            if (part == "mail") db.RoleMails.Add(new RoleMail { RoleId = 2, MailId = 1, Items = "{" });
            db.SaveChanges();
        }
        Assert.NotEqual(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Same(current, ctx.Player);
        Assert.Equal(1, ctx.Player.Roles.Active()!.Id);
    }

    [Fact]
    public async Task FailedSave_RollsBackAllTables_AndRetriesWithoutLosingDirtyState()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var current = ctx.Player;
        var before = current.Wallet.Balance(1);
        current.Wallet.Credit(1, 42);
        current.GrantRewards([new ItemGrant(12031001, 1)], EnmItemReason.EnmItemChangeNormal);
        var motive = Assert.Single(current.Motives.All);
        var character = current.Characters.All.First();
        Assert.Equal(0, current.EquipMotive(motive.UniqId, character.InstId));
        using (var db = new GameDbContext(_options))
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_snapshot BEFORE UPDATE ON role_saves BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");

        Assert.NotEqual(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Same(current, ctx.Player);
        Assert.True(current.Characters.IsDirty);
        Assert.True(current.Motives.IsDirty);
        Assert.True(current.SaveDirty);
        using (var db = new GameDbContext(_options))
        {
            Assert.Empty(await db.RoleMotives.ToArrayAsync());
            Assert.All(await db.RoleCharacters.ToArrayAsync(), row => Assert.Equal(0, row.MotiveUniqId));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_snapshot;");
        }
        await _store.SaveAsync(current);
        Assert.False(current.IsDirty);
        var loaded = await _store.LoadAsync(current, 1, DateTimeOffset.UtcNow);
        Assert.Equal(before + 42, loaded.Wallet.Balance(1));
        Assert.Equal(motive.UniqId, loaded.Characters.Get(character.InstId)!.MotiveUniqId);
        Assert.Equal(character.InstId, loaded.Motives.Get(motive.UniqId)!.EquipedTarget);
    }

    [Fact]
    public async Task FullMailbox_PreservesOverflowAcrossReload_ThenDeliversOnce()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var now = DateTimeOffset.UtcNow;
        ctx.Player.Bag.Add(21206001, _assets.Items.Get(21206001)!.HoldLimit);
        ctx.Player.Mails.Load(Enumerable.Range(1, ctx.Player.Mails.BoxCap).Select(id => new MailEntry {
            MailId = (uint)id, Items = [new ItemGrant(21206001, 1)],
            ExpireTime = id == 1 ? (uint)now.AddMinutes(1).ToUnixTimeSeconds() : 0
        }));
        ctx.Player.Guid.Adopt((ulong)ctx.Player.Mails.BoxCap);
        var factory = new TestFactory(_options);
        await new MailRepository(factory, NullLogger<MailRepository>.Instance)
            .SaveAsync(1, ctx.Player.Mails.Entries);

        var result = ctx.Player.GrantRewards([new ItemGrant(21206001, 2)], EnmItemReason.EnmItemChangeNormal);
        Assert.Empty(result.Undelivered);
        Assert.Empty(result.Mailed);
        Assert.Equal(2u, Assert.Single(result.Deferred).Count);
        Assert.Single(ctx.Player.PendingRewardMail);
        await _store.SaveAsync(ctx.Player);
        var loaded = await _store.LoadAsync(ctx.Player, 1, now);
        Assert.Equal(2u, Assert.Single(Assert.Single(loaded.PendingRewardMail)).Count);

        loaded.AdvanceTime(now.AddMinutes(2));
        Assert.Empty(loaded.PendingRewardMail);
        var delivery = Assert.Single(loaded.DrainGameplayChanges().OfType<SCMailAddDelNft>(), n => n.AddMails.Count > 0);
        Assert.Single(delivery.AddMails);
        Assert.Equal(2u, Assert.Single(loaded.Mails.Entries.Last().Items).Count);
        await _store.SaveAsync(loaded);
        var reloaded = await _store.LoadAsync(loaded, 1, now.AddMinutes(2));
        Assert.Empty(reloaded.PendingRewardMail);
        reloaded.AdvanceTime(now.AddMinutes(2));
        Assert.DoesNotContain(reloaded.DrainGameplayChanges().OfType<SCMailAddDelNft>(), n => n.AddMails.Count > 0);
        Assert.Equal(_assets.GlobalConfig.MailMaxSaveCount, reloaded.Mails.Entries.Count);
    }

    [Fact]
    public async Task Logout_KeepsAccount_AndRejectsGameplayUntilRoleLogin()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.NotEqual(0, await _sessions.LogoutAsync(ctx, 2));
        Assert.True(ctx.Player.HasActiveRole);
        Assert.Equal(0, await _sessions.LogoutAsync(ctx, 1));
        Assert.True(ctx.Player.IsLoggedIn);
        Assert.False(ctx.Player.HasActiveRole);
        Assert.Empty(ctx.Player.InventoryItems());
        var reply = await DispatchReplyAsync(ctx, outbound, new CSCharacterUpdateTeam(), SCCharacterUpdateTeam.Parser);
        Assert.Equal((int)EnmTextCode.EnmTextNotAccLogin, reply.Result);
    }

    public void Dispose()
    {
        _metrics.Dispose();
        _connection.Dispose();
    }

    private sealed class TestFactory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext() => new(options);
        public Task<GameDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
