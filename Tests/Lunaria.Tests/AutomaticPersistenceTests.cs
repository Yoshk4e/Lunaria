using System.Data.Common;
using System.Reflection;
using System.Text.Json.Serialization;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Saves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public void AutomaticTracking_EverySavePropertyHasExactlyOneSection()
    {
        var properties = typeof(RoleSaveDocument).GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name).Order().ToArray();
        Assert.Equal(properties, RoleSaveMapper.Sections.Select(s => s.Name).Order().ToArray());
    }

    [Fact]
    public async Task AutomaticTracking_RosterReplacementUpdatesOnlyChangedCharacterColumns()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var first = ctx.Player.Characters.All[0];
        var commands = new PersistenceCommands();
        ctx.Player.Characters.Load(ctx.Player.Characters.All.Select(c =>
            c.InstId == first.InstId ? c with { Level = c.Level + 1 } : c).ToArray());
        await RecordingStore(commands).SaveAsync(ctx.Player);
        var update = Assert.Single(commands.Commands, c => c.Sql.Contains("UPDATE \"role_characters\""));
        Assert.Contains("\"level\" =", update.Sql);
        Assert.DoesNotContain("\"exp\" =", update.Sql);
        Assert.DoesNotContain("\"motive_uniq_id\" =", update.Sql);
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(first.Level + 1, loaded.Characters.Get(first.InstId)!.Level);
    }

    [Fact]
    public async Task AutomaticTracking_SectionFailureRollsBackEarlierWritesAndRetryKeepsAllChanges()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.Bag.Add(21206001, 2);
        await _store.SaveAsync(ctx.Player);
        Dictionary<string, string> before;
        await using (var db = new GameDbContext(_options))
        {
            before = await db.RoleSaveSections.Where(s => s.RoleId == 1).ToDictionaryAsync(s => s.Name, s => s.State);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_wallet_section BEFORE UPDATE ON role_save_sections WHEN NEW.name = 'wallet' BEGIN SELECT RAISE(ABORT, 'injected section failure'); END;");
        }
        ctx.Player.Bag.All().First(s => s.ItemId == 21206001).Count--;
        ctx.Player.Wallet.Credit(1, 17);
        var money = ctx.Player.Wallet.Balance(1);
        var items = ctx.Player.Bag.CountOf(21206001);
        await Assert.ThrowsAsync<DbUpdateException>(() => _store.SaveAsync(ctx.Player));
        Assert.True(ctx.Player.IsDirty);
        await using (var db = new GameDbContext(_options))
        {
            var after = await db.RoleSaveSections.Where(s => s.RoleId == 1).ToDictionaryAsync(s => s.Name, s => s.State);
            Assert.Equal(before.OrderBy(p => p.Key), after.OrderBy(p => p.Key));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_wallet_section;");
        }
        await _store.SaveAsync(ctx.Player);
        Assert.False(ctx.Player.IsDirty);
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(money, loaded.Wallet.Balance(1));
        Assert.Equal(items, loaded.Bag.CountOf(21206001));
    }

    [Fact]
    public async Task SectionMigration_DowngradeAndUpgradePreserveTheFullSave()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.Wallet.Credit(1, 19);
        await _store.SaveAsync(ctx.Player);
        var expected = ctx.Player.Wallet.Balance(1);
        await using (var db = new GameDbContext(_options))
        {
            var previous = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("SplitRoleSaveSections")).Last();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(previous);
            var legacy = await db.RoleSaves.AsNoTracking().SingleAsync(s => s.RoleId == 1);
            Assert.Contains("\"wallet\"", legacy.State);
            await migrator.MigrateAsync();
        }
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(expected, loaded.Wallet.Balance(1));
        await _store.SaveAsync(loaded);
        await using var verify = new GameDbContext(_options);
        Assert.True(await verify.RoleSaveSections.AnyAsync(s => s.RoleId == 1 && s.Name == "wallet"));
    }

    [Fact]
    public async Task AutomaticTracking_WalletWritesOnlyItsSection_AndNoOpsIssueNoCommands()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var commands = new PersistenceCommands();
        var store = RecordingStore(commands);
        var original = ctx.Player.Wallet.Balance(1);
        ctx.Player.Wallet.Credit(1, 7);
        await store.SaveAsync(ctx.Player);
        var sectionUpdate = Assert.Single(commands.Commands, c => c.Sql.Contains("UPDATE \"role_save_sections\""));
        Assert.Contains("wallet", sectionUpdate.Parameters);
        Assert.DoesNotContain(commands.Commands, c => c.Sql.Contains("role_characters") || c.Sql.Contains("role_motives") || c.Sql.Contains("role_mails"));
        Assert.False(ctx.Player.IsDirty);

        commands.Commands.Clear();
        ctx.Player.Wallet.Debit(1, 3);
        ctx.Player.Wallet.Credit(1, 3);
        Assert.False(ctx.Player.IsDirty);
        await store.SaveAsync(ctx.Player);
        Assert.Empty(commands.Commands);
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(original + 7, loaded.Wallet.Balance(1));
    }

    [Fact]
    public async Task AutomaticTracking_DirectStackMutationSurvivesReload()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.Bag.Add(21206001, 2);
        await _store.SaveAsync(ctx.Player);
        var stack = ctx.Player.Bag.All().First(s => s.ItemId == 21206001);
        var before = stack.Count;
        stack.Count--;
        Assert.True(ctx.Player.IsDirty);
        await _store.SaveAsync(ctx.Player);
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(before - 1, loaded.Bag.CountOf(21206001));
    }

    [Fact]
    public async Task AutomaticTracking_CounterOnlyDoesNotReadOrWriteRoster()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var commands = new PersistenceCommands();
        var store = RecordingStore(commands);
        var minted = ctx.Player.Guid.Next();
        await store.SaveAsync(ctx.Player);
        Assert.DoesNotContain(commands.Commands, c => c.Sql.Contains("role_characters") || c.Sql.Contains("role_save_sections"));
        Assert.Single(commands.Commands, c => c.Sql.Contains("UPDATE \"roles\""));
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(minted, loaded.Guid.LastMinted);
    }

    [Fact]
    public async Task AutomaticTracking_LiveLoadIsAChange_WhileDatabaseHydrationIsClean()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        ctx.Player.Wallet.Load([(1, 1234L)]);
        Assert.True(ctx.Player.IsDirty);
        await _store.SaveAsync(ctx.Player);
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(1234L, loaded.Wallet.Balance(1));
        Assert.False(loaded.Wallet.IsDirty);
    }

    private RoleStateStore RecordingStore(PersistenceCommands commands)
    {
        var factory = new TestFactory(new DbContextOptionsBuilder<GameDbContext>(_options).AddInterceptors(commands).Options);
        return new RoleStateStore(factory,
            new RoleRepository(factory, NullLogger<RoleRepository>.Instance),
            new CharacterRepository(factory, NullLogger<CharacterRepository>.Instance),
            new MotiveRepository(factory, NullLogger<MotiveRepository>.Instance),
            new GuideRepository(factory, NullLogger<GuideRepository>.Instance),
            new MailRepository(factory, NullLogger<MailRepository>.Instance),
            new RoleSaveRepository(factory, NullLogger<RoleSaveRepository>.Instance));
    }

    private sealed class PersistenceCommands : DbCommandInterceptor
    {
        public List<(string Sql, string[] Parameters)> Commands { get; } = [];
        private void Record(DbCommand command) => Commands.Add((command.CommandText,
            command.Parameters.Cast<DbParameter>().Select(p => p.Value?.ToString() ?? "").ToArray()));
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
    }
}
