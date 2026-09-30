using System.Data.Common;
using System.Diagnostics;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Entities;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;

if (args.Length < 3) throw new ArgumentException("Usage: <mode> <database> <assets> [expected coins] [expected motives]");
var mode = args[0];
var assets = new GameData(args[2]);
await assets.StartAsync(default);
var options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite($"Data Source={args[1]};Pooling=False");
if (mode == "uncommitted") options.AddInterceptors(new StopBeforeSnapshot());
var factory = new Factory(options.Options);
var store = new RoleStateStore(factory,
    new RoleRepository(factory, NullLogger<RoleRepository>.Instance),
    new CharacterRepository(factory, NullLogger<CharacterRepository>.Instance),
    new MotiveRepository(factory, NullLogger<MotiveRepository>.Instance),
    new GuideRepository(factory, NullLogger<GuideRepository>.Instance),
    new MailRepository(factory, NullLogger<MailRepository>.Instance),
    new RoleSaveRepository(factory, NullLogger<RoleSaveRepository>.Instance));

Player Session(int id)
{
    var session = new Player((ulong)id, assets);
    session.Account.Bind(id, $"probe-{id}", $"probe-{id}", DateTimeOffset.UtcNow);
    session.Roles.Load([new RoleRow(id, id, 0, "", "", 0, false)]);
    return session;
}

if (mode == "init")
{
    await using (var db = factory.CreateDbContext())
    {
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        for (var id = 1; id <= 8; id++)
        {
            db.Accounts.Add(new Account { Id = id, AccountKey = $"probe-{id}", Userid = $"probe-{id}" });
            db.Roles.Add(new Role { Id = id, AccountId = id, Slot = 0 });
        }
        await db.SaveChangesAsync();
    }
    for (var id = 1; id <= 8; id++)
    {
        var player = await store.LoadAsync(Session(id), id, DateTimeOffset.UtcNow);
        player.Wallet.Debit(1, player.Wallet.Balance(1));
        await store.SaveAsync(player);
    }
    Console.WriteLine("INITIALIZED");
}
else if (mode is "commit" or "uncommitted")
{
    var player = await store.LoadAsync(Session(1), 1, DateTimeOffset.UtcNow);
    player.GrantRewards([new ItemGrant(1, 42), new ItemGrant(12031001, 1)], EnmItemReason.EnmItemChangeNormal);
    await store.SaveAsync(player);
    Console.WriteLine("READY");
    Console.Out.Flush();
    await Task.Delay(Timeout.Infinite);
}
else if (mode == "verify")
{
    var player = await store.LoadAsync(Session(1), 1, DateTimeOffset.UtcNow);
    var coins = long.Parse(args[3]);
    var motives = int.Parse(args[4]);
    if (player.Wallet.Balance(1) != coins || player.Motives.All.Count() != motives)
        throw new InvalidOperationException($"Expected {coins} coins/{motives} motives; found {player.Wallet.Balance(1)}/{player.Motives.All.Count()}");
    Console.WriteLine($"VERIFIED {coins} coins, {motives} motives");
}
else if (mode == "concurrent")
{
    var timer = Stopwatch.StartNew();
    await Task.WhenAll(Enumerable.Range(1, 8).Select(id => Task.Run(async () => {
        var player = await store.LoadAsync(Session(id), id, DateTimeOffset.UtcNow);
        for (var operation = 0; operation < 20; operation++)
        {
            player.GrantRewards([new ItemGrant(1, 1)], EnmItemReason.EnmItemChangeNormal);
            await store.SaveAsync(player);
        }
        var reloaded = await store.LoadAsync(Session(id), id, DateTimeOffset.UtcNow);
        if (reloaded.Wallet.Balance(1) != 20) throw new InvalidOperationException($"Role {id} lost or duplicated a commit");
    })));
    Console.WriteLine($"VERIFIED 8 concurrent roles, 160 commits, {timer.Elapsed.TotalSeconds:F2}s");
}
else throw new ArgumentException($"Unknown mode {mode}");

sealed class Factory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext() => new(options);
    public Task<GameDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}

sealed class StopBeforeSnapshot : DbCommandInterceptor
{
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("UPDATE \"role_saves\"", StringComparison.Ordinal))
        {
            Console.WriteLine("READY");
            Console.Out.Flush();
            // The driver kills this process before the roster and motive writes commit.
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        return result;
    }
}
