using System.Text.Json;
using System.Text.Json.Nodes;
using Lunaria.Game.Player.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence.Saves;

public sealed class RoleSaveRepository(IDbContextFactory<GameDbContext> factory, ILogger<RoleSaveRepository> logger)
{
    public async Task<bool> LoadIntoAsync(long roleId, Player player, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadIntoAsync(db, roleId, player, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> LoadIntoAsync(GameDbContext db, long roleId, Player player, CancellationToken cancellationToken = default)
    {
        var blob = await ReadDocumentAsync(db, roleId, cancellationToken);
        player.SaveBaseline.RoleId = roleId;
        if (blob is null) return false;
        try
        {
            var document = RoleSaveMigrations.Read(blob, player.UtcNow);
            RoleSaveMapper.Apply(player, document);
            var sections = await db.RoleSaveSections.AsNoTracking().Where(s => s.RoleId == roleId)
                .ToDictionaryAsync(s => s.Name, s => s.State, cancellationToken);
            player.SaveBaseline.HasSections = sections.Count > 0;
            foreach (var (name, state) in sections) player.SaveBaseline.Sections[name] = state;
            // Remember repairs made during loading so the next save writes them back.
            foreach (var section in RoleSaveMapper.Sections)
            {
                var current = section.Capture(player);
                if (!sections.TryGetValue(section.Name, out var stored) || !RoleSaveBaseline.JsonEqual(stored, current))
                    player.SaveBaseline.Repairs.Add(section.Name);
            }
            return true;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Save for role {roleId} is corrupt.", ex);
        }
    }

    /// <summary>Read both legacy documents and saves stored as separate sections.</summary>
    internal static async Task<string?> ReadDocumentAsync(GameDbContext db, long roleId, CancellationToken cancellationToken = default)
    {
        var legacy = await db.RoleSaves.AsNoTracking().Where(s => s.RoleId == roleId)
            .Select(s => s.State).SingleOrDefaultAsync(cancellationToken);
        if (legacy is null) return null;
        try
        {
            RoleSaveMigrations.CheckVersion(legacy);
            var sections = await db.RoleSaveSections.AsNoTracking().Where(s => s.RoleId == roleId)
                .ToDictionaryAsync(s => s.Name, s => s.State, cancellationToken);
            if (sections.Count == 0) return legacy;
            if (!sections.ContainsKey("schema_version")) throw new JsonException("Sectioned save has no schema version.");
            var document = new JsonObject();
            foreach (var (name, state) in sections) document.Add(name, JsonNode.Parse(state));
            return document.ToJsonString();
        }
        catch (JsonException ex) { throw new InvalidDataException($"Save for role {roleId} is corrupt.", ex); }
    }

    public async Task SaveAsync(long roleId, Player player, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var stored = await db.RoleSaves.AsNoTracking().Where(s => s.RoleId == roleId)
            .Select(s => s.State).SingleOrDefaultAsync(cancellationToken);
        if (stored is not null) RoleSaveMigrations.CheckVersion(stored);
        var sameRole = player.SaveBaseline.RoleId == roleId;
        var changes = sameRole ? player.SaveBaseline.CaptureChanges(player)
            : RoleSaveMapper.Sections.ToDictionary(s => s.Name, s => s.Capture(player));
        if (changes.Count == 0) return;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await WriteAsync(db, roleId, changes, player.UtcNow.UtcDateTime, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (sameRole) player.SaveBaseline.Accept(changes);
    }

    internal async Task WriteAsync(GameDbContext db, long roleId, IReadOnlyDictionary<string, string> changes,
        DateTime now, CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0) return;
        var header = await db.RoleSaves.SingleOrDefaultAsync(s => s.RoleId == roleId, cancellationToken);
        if (header is null)
        {
            header = new RoleSave { RoleId = roleId };
            db.RoleSaves.Add(header);
        }
        else RoleSaveMigrations.CheckVersion(header.State);
        // Keep the legacy row so callers can check whether a save exists and read its version.
        header.State = JsonSerializer.Serialize(new { schema_version = RoleSaveMigrations.CurrentVersion });
        header.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        var names = changes.Keys.ToArray();
        var rows = await db.RoleSaveSections.Where(s => s.RoleId == roleId && names.Contains(s.Name))
            .ToDictionaryAsync(s => s.Name, cancellationToken);
        foreach (var (name, state) in changes)
        {
            if (!rows.TryGetValue(name, out var row))
            {
                row = new RoleSaveSection { RoleId = roleId, Name = name };
                db.RoleSaveSections.Add(row);
            }
            row.State = state;
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogDebug("role {RoleId} saved {Sections} changed sections", roleId, changes.Count);
    }
}
