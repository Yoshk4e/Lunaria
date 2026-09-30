using Lunaria.Game.Characters;
using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

public sealed class CharacterRepository(
    IDbContextFactory<GameDbContext> factory,
    ILogger<CharacterRepository> logger
)
{
    public async Task<(IReadOnlyList<CharacterState> Roster, ulong LastMinted)> LoadAsync(
        long roleId,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadAsync(db, roleId, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<(IReadOnlyList<CharacterState> Roster, ulong LastMinted)> LoadAsync(GameDbContext db,
        long roleId,
        CancellationToken cancellationToken = default
    )
    {

        var counter = await db.Roles.AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => r.LastMintedInstId)
            .SingleOrDefaultAsync(cancellationToken);

        var roster = await db.RoleCharacters.AsNoTracking()
            .Where(c => c.RoleId == roleId)
            .OrderBy(c => c.InstId)
            .Select(c => new CharacterState {
                InstId = (ulong)c.InstId,
                CharacterId = c.CharacterId,
                Level = c.Level,
                Exp = c.Exp,
                BreakLevel = c.BreakLevel,
                MotiveUniqId = (ulong)c.MotiveUniqId
            })
            .ToListAsync(cancellationToken);
        return (roster, (ulong)counter);
    }

    public async Task SaveAsync(
        long roleId,
        IReadOnlyList<CharacterState> roster,
        ulong lastMintedInstId,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, roleId, roster, lastMintedInstId, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAsync(GameDbContext db,
        long roleId,
        IReadOnlyList<CharacterState> roster,
        ulong lastMintedInstId,
        CancellationToken cancellationToken = default
    )
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
            return;

        role.LastMintedInstId = (long)lastMintedInstId;

        var rows = await db.RoleCharacters
            .Where(c => c.RoleId == roleId)
            .ToListAsync(cancellationToken);

        var live = roster.ToDictionary(c => c.InstId);

        foreach (var row in rows)
        {
            if (!live.TryGetValue((ulong)row.InstId, out var state))
            {
                db.RoleCharacters.Remove(row);
                continue;
            }

            row.CharacterId = state.CharacterId;
            row.Level = state.Level;
            row.Exp = state.Exp;
            row.BreakLevel = state.BreakLevel;
            row.MotiveUniqId = (long)state.MotiveUniqId;
        }

        var stored = rows.Select(r => (ulong)r.InstId).ToHashSet();

        foreach (var state in roster)
        {
            if (stored.Contains(state.InstId))
                continue;

            db.RoleCharacters.Add(new RoleCharacter {
                RoleId = roleId,
                InstId = (long)state.InstId,
                CharacterId = state.CharacterId,
                Level = state.Level,
                Exp = state.Exp,
                BreakLevel = state.BreakLevel,
                MotiveUniqId = (long)state.MotiveUniqId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogDebug("role {RoleId} roster saved ({Count} characters)", roleId, roster.Count);
    }
}
