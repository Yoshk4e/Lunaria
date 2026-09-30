using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Lunaria.Game.Player.Persistence;

public sealed class AccountRepository(IDbContextFactory<GameDbContext> factory)
{
    public async Task<AccountRow> UpsertOnLoginAsync(
        string accountKey,
        string channelName,
        string channelUid,
        string udid,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var existing = await db.Accounts.SingleOrDefaultAsync(a => a.AccountKey == accountKey, cancellationToken);

        if (existing is not null)
        {
            existing.ChannelName = channelName;
            existing.ChannelUid = channelUid;
            existing.Udid = udid;
            existing.LastLoginAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return new AccountRow(existing.Id, existing.AccountKey, existing.Userid);
        }

        var account = new Account {
            AccountKey = accountKey,
            Userid = Guid.CreateVersion7().ToString(),
            ChannelName = channelName,
            ChannelUid = channelUid,
            Udid = udid,
            CreatedAt = now,
            LastLoginAt = now
        };
        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another login already created this account.
            var raced = await db.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(a => a.AccountKey == accountKey, cancellationToken);

            if (raced is not null)
                return new AccountRow(raced.Id, raced.AccountKey, raced.Userid);

            throw;
        }

        return new AccountRow(account.Id, account.AccountKey, account.Userid);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 }; // SQLITE_CONSTRAINT
}
