using Lunaria.Game.Player.Managers;
using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

public sealed class RoleRepository(
    IDbContextFactory<GameDbContext> factory,
    ILogger<RoleRepository> logger
)
{
    public async Task<IReadOnlyList<RoleRow>> ListByAccountAsync(long accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Roles.AsNoTracking()
            .Where(r => r.AccountId == accountId)
            .OrderBy(r => r.Id)
            .Select(r => new RoleRow(r.Id, r.AccountId, r.Slot, r.Name, r.SecondName, r.Gender, r.Initialized))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleRow> CreateAsync(
        long accountId,
        long slot,
        string name,
        string secondName,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var role = new Role {
            AccountId = accountId,
            Slot = slot,
            Name = name,
            SecondName = secondName,
            Gender = 0,
            Initialized = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Roles.Add(role);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw ClassifyUnique(ex);
        }

        logger.LogInformation("role {RoleId} created for account {AccountId} in slot {Slot}", role.Id, accountId, slot);
        return new RoleRow(role.Id, role.AccountId, role.Slot, role.Name, role.SecondName, role.Gender, role.Initialized);
    }

    public async Task SaveAsync(RoleState role, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, role, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAsync(GameDbContext db, RoleState role, CancellationToken cancellationToken = default)
    {
        var row = await db.Roles.SingleOrDefaultAsync(r => r.Id == role.Id, cancellationToken);

        if (row is null)
            return;

        row.Name = role.Name;
        row.SecondName = role.SecondName;
        row.Gender = role.Gender;
        row.Initialized = role.Initialized;
        row.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw ClassifyUnique(ex);
        }
    }

    public async Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Roles.AsNoTracking()
            .AnyAsync(r => r.Initialized && r.Name == name, cancellationToken);
    }

    private static RoleRepositoryException ClassifyUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        if (message.Contains("idx_roles_name") || message.Contains("roles.name"))
            throw new RoleRepositoryException(RoleRepositoryError.NameTaken);

        if (message.Contains("roles.slot") || message.Contains("IX_roles_account_id_slot")
                                           || message.Contains("roles.AccountId"))
            throw new RoleRepositoryException(RoleRepositoryError.CapReached);

        throw ex;
    }
}
