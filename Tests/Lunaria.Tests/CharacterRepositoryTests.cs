using Lunaria.Game.Characters;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lunaria.Tests;

public sealed class CharacterRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<GameDbContext> _options;

    public CharacterRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(_connection).Options;
        using var db = new GameDbContext(_options);
        db.Database.Migrate();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private CharacterRepository Repo() =>
        new(new TestFactory(_options), NullLogger<CharacterRepository>.Instance);

    private long NewRole()
    {
        using var db = new GameDbContext(_options);
        var account = new Account { AccountKey = Guid.NewGuid().ToString(), Userid = Guid.NewGuid().ToString() };
        db.Accounts.Add(account);
        db.SaveChanges();
        var role = new Role { AccountId = account.Id, Slot = 0 };
        db.Roles.Add(role);
        db.SaveChanges();
        return role.Id;
    }

    private static CharacterState State(ulong instId, uint characterId = 1001, uint level = 1) => new() {
        InstId = instId,
        CharacterId = characterId,
        Level = level
    };

    [Fact]
    public async Task Load_UnknownRole_IsEmptyWithZeroCounter()
    {
        var (roster, lastMinted) = await Repo().LoadAsync(12345);

        Assert.Empty(roster);
        Assert.Equal(expected: 0UL, lastMinted);
    }

    [Fact]
    public async Task SaveAndLoad_RoundtripsEveryField()
    {
        var roleId = NewRole();
        var repo = Repo();

        await repo.SaveAsync(roleId,
            [State(instId: 1, characterId: 1001, level: 5) with { Exp = 50, BreakLevel = 1, MotiveUniqId = 77 }],
            lastMintedInstId: 1);

        var (roster, lastMinted) = await repo.LoadAsync(roleId);

        var single = Assert.Single(roster);
        Assert.Equal(expected: 1UL, single.InstId);
        Assert.Equal(expected: 1001u, single.CharacterId);
        Assert.Equal(expected: 5u, single.Level);
        Assert.Equal(expected: 50u, single.Exp);
        Assert.Equal(expected: 1u, single.BreakLevel);
        Assert.Equal(expected: 77UL, single.MotiveUniqId);
        Assert.Equal(expected: 1UL, lastMinted);
    }

    [Fact]
    public async Task Save_Reconciles_UpdatesEditsDeletesRemovalsAddsNew()
    {
        var roleId = NewRole();
        var repo = Repo();

        await repo.SaveAsync(roleId, [State(instId: 1, level: 1), State(instId: 2, level: 1), State(instId: 3, level: 1)],
            lastMintedInstId: 3);

        await repo.SaveAsync(roleId, [State(instId: 1, level: 20), State(instId: 3, level: 1), State(instId: 4, characterId: 1002)],
            lastMintedInstId: 4);

        var (roster, lastMinted) = await repo.LoadAsync(roleId);
        Assert.Equal([1UL, 3UL, 4UL], roster.Select(c => c.InstId).ToList());
        Assert.Equal(expected: 20u, roster[0].Level);
        Assert.Equal(expected: 1002u, roster[2].CharacterId);
        Assert.Equal(expected: 4UL, lastMinted);
    }

    [Fact]
    public async Task Counter_SurvivesRowDeletion_NeverReusesIds()
    {
        var roleId = NewRole();
        var repo = Repo();
        await repo.SaveAsync(roleId, [State(1), State(2), State(3)], lastMintedInstId: 3);

        await repo.SaveAsync(roleId, [State(1)], lastMintedInstId: 3);

        var (roster, lastMinted) = await repo.LoadAsync(roleId);
        Assert.Single(roster);
        Assert.Equal(expected: 3UL, lastMinted);
    }

    [Fact]
    public async Task Saves_ArePerRole()
    {
        var first = NewRole();
        var second = NewRole();
        var repo = Repo();
        await repo.SaveAsync(first, [State(1)], lastMintedInstId: 1);

        var (firstRoster, firstCounter) = await repo.LoadAsync(first);
        var (secondRoster, secondCounter) = await repo.LoadAsync(second);
        Assert.Single(firstRoster);
        Assert.Equal(expected: 1UL, firstCounter);
        Assert.Empty(secondRoster);
        Assert.Equal(expected: 0UL, secondCounter);
    }

    private sealed class TestFactory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext() => new(options);

        public Task<GameDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
