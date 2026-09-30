using System.Text.Json;
using Lunaria.Game.Guide;
using Lunaria.Game.Player.Persistence.Entities;
using Lunaria.Game.Player.Persistence.Saves;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

/// <summary>Reject invalid saves so an empty handbook cannot overwrite them.</summary>
public sealed class GuideRepository(IDbContextFactory<GameDbContext> factory, ILogger<GuideRepository> logger)
{
    public async Task<IReadOnlyDictionary<uint, GuideEntry>> LoadAsync(
        long roleId,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadAsync(db, roleId, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<IReadOnlyDictionary<uint, GuideEntry>> LoadAsync(GameDbContext db,
        long roleId,
        CancellationToken cancellationToken = default
    )
    {
        var blob = await db.RoleGuides.AsNoTracking()
            .Where(g => g.RoleId == roleId)
            .Select(g => g.Entries)
            .SingleOrDefaultAsync(cancellationToken);

        if (blob is null)
            return new Dictionary<uint, GuideEntry>();

        try
        {
            return JsonSerializer.Deserialize<GuideBlob>(blob, SaveJson.Options)?.Entries
                   ?? throw new InvalidDataException("Invalid guide save.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Cannot load guide save for role {RoleId}", roleId);
            throw new InvalidDataException($"Guide save for role {roleId} is corrupt.", ex);
        }
    }

    public async Task SaveAsync(long roleId, GuideManager guides, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, roleId, guides, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAsync(GameDbContext db, long roleId, GuideManager guides, CancellationToken cancellationToken = default)
    {
        var blob = new GuideBlob { Entries = guides.Entries.ToDictionary(kv => kv.Key, kv => kv.Value) };
        var entries = JsonSerializer.Serialize(blob, SaveJson.Options);

        var row = await db.RoleGuides.SingleOrDefaultAsync(g => g.RoleId == roleId, cancellationToken);

        if (row is null)
        {
            db.RoleGuides.Add(new RoleGuide { RoleId = roleId, Entries = entries, UpdatedAt = DateTime.UtcNow });
        } else
        {
            row.Entries = entries;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
