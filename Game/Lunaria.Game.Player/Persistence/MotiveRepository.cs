using Lunaria.Game.Motives;
using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

public sealed class MotiveRepository(
    IDbContextFactory<GameDbContext> factory,
    ILogger<MotiveRepository> logger
)
{
    public async Task<IReadOnlyList<MotiveState>> LoadAsync(
        long roleId,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadAsync(db, roleId, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<IReadOnlyList<MotiveState>> LoadAsync(GameDbContext db,
        long roleId,
        CancellationToken cancellationToken = default
    )
    {

        var motives = await db.RoleMotives.AsNoTracking()
            .Where(m => m.RoleId == roleId)
            .OrderBy(m => m.UniqId)
            .Select(m => new MotiveState {
                UniqId = (ulong)m.UniqId,
                MotiveId = m.MotiveId,
                ItemId = m.ItemId,
                ClaimTime = (ulong)m.ClaimTime,
                Level = m.Level,
                Exp = m.Exp,
                RefineLevel = m.RefineLevel,
                BreakLevel = m.BreakLevel,
                Locked = m.Locked,
                EquipedTarget = (ulong)m.EquipedTarget
            })
            .ToListAsync(cancellationToken);
        return motives;
    }

    public async Task SaveAsync(
        long roleId,
        IReadOnlyList<MotiveState> motives,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, roleId, motives, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAsync(GameDbContext db,
        long roleId,
        IReadOnlyList<MotiveState> motives,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<ulong>? changedIds = null
    )
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
            return;

        var ids = changedIds?.Select(id => (long)id).ToArray();
        var rows = await db.RoleMotives
            .Where(m => m.RoleId == roleId && (ids == null || ids.Contains(m.UniqId)))
            .ToListAsync(cancellationToken);

        motives = changedIds is null ? motives : motives.Where(s => changedIds.Contains(s.UniqId)).ToArray();
        var live = motives.ToDictionary(m => m.UniqId);

        foreach (var row in rows)
        {
            if (!live.TryGetValue((ulong)row.UniqId, out var state))
            {
                db.RoleMotives.Remove(row);
                continue;
            }

            WriteRow(row, roleId, state);
        }

        var stored = rows.Select(r => (ulong)r.UniqId).ToHashSet();

        foreach (var state in motives)
        {
            if (stored.Contains(state.UniqId))
                continue;

            var row = new RoleMotive();
            WriteRow(row, roleId, state);
            db.RoleMotives.Add(row);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogDebug("role {RoleId} motives saved ({Count} motives)", roleId, motives.Count);
    }

    private static void WriteRow(RoleMotive row, long roleId, MotiveState state)
    {
        row.RoleId = roleId;
        row.UniqId = (long)state.UniqId;
        row.MotiveId = state.MotiveId;
        row.ItemId = state.ItemId;
        row.ClaimTime = (long)state.ClaimTime;
        row.Level = state.Level;
        row.Exp = state.Exp;
        row.RefineLevel = state.RefineLevel;
        row.BreakLevel = state.BreakLevel;
        row.Locked = state.Locked;
        row.EquipedTarget = (long)state.EquipedTarget;
    }
}
