using System.Text.Json;
using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence.Saves;

/// <summary>Missing saves start a new role. Reject corrupt saves to protect existing progress.</summary>
public sealed class RoleSaveRepository(
    IDbContextFactory<GameDbContext> factory,
    ILogger<RoleSaveRepository> logger
)
{
    public async Task<bool> LoadIntoAsync(long roleId, Player player, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadIntoAsync(db, roleId, player, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> LoadIntoAsync(GameDbContext db, long roleId, Player player, CancellationToken cancellationToken = default)
    {

        var blob = await db.RoleSaves.AsNoTracking()
            .Where(s => s.RoleId == roleId)
            .Select(s => s.State)
            .SingleOrDefaultAsync(cancellationToken);

        if (blob is null)
            return false;

        RoleSaveDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<RoleSaveDocument>(blob, SaveJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Save for role {roleId} is corrupt.", ex);
        }

        if (document is null)
            throw new InvalidDataException($"Save for role {roleId} is empty.");

        RoleSaveMapper.Apply(player, document);
        return true;
    }

    public async Task SaveAsync(long roleId, Player player, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, roleId, player, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAsync(GameDbContext db, long roleId, Player player, CancellationToken cancellationToken = default)
    {
        var state = JsonSerializer.Serialize(RoleSaveMapper.Capture(player), SaveJson.Options);

        var row = await db.RoleSaves.SingleOrDefaultAsync(s => s.RoleId == roleId, cancellationToken);

        if (row is null)
        {
            db.RoleSaves.Add(new RoleSave { RoleId = roleId, State = state, UpdatedAt = DateTime.UtcNow });
        } else
        {
            row.State = state;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogDebug("role {RoleId} save written ({Bytes} bytes)", roleId, state.Length);
    }
}
