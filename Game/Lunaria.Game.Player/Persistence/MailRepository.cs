using System.Text.Json;
using Lunaria.Game.Mail;
using Lunaria.Game.Player.Persistence.Entities;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

public sealed class MailRepository(
    IDbContextFactory<GameDbContext> factory,
    ILogger<MailRepository> logger
)
{
    public async Task<IReadOnlyList<MailEntry>> LoadAsync(
        long roleId,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadAsync(db, roleId, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<IReadOnlyList<MailEntry>> LoadAsync(GameDbContext db,
        long roleId,
        CancellationToken cancellationToken = default
    )
    {

        var rows = await db.RoleMails.AsNoTracking()
            .Where(m => m.RoleId == roleId)
            .OrderBy(m => m.MailId)
            .ToListAsync(cancellationToken);

        var mails = new List<MailEntry>(rows.Count);

        foreach (var row in rows)
        {
            mails.Add(new MailEntry {
                MailId = unchecked((uint)row.MailId),
                TemplateId = row.TemplateId,
                Type = row.Type,
                Important = row.Important,
                Open = row.Open,
                Items = ParseItems(row, roleId),
                TemplateContentParams = ParseText<string>(row.TemplateContentParams, row.MailId, roleId),
                Contents = ParseText<MailText>(row.Contents, row.MailId, roleId),
                Time = unchecked((uint)row.Time),
                ExpireTime = unchecked((uint)row.ExpireTime)
            });
        }

        return mails;
    }

    public async Task SaveAsync(
        long roleId,
        IReadOnlyList<MailEntry> mails,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, roleId, mails, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAsync(GameDbContext db,
        long roleId,
        IReadOnlyList<MailEntry> mails,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<uint>? changedIds = null
    )
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
            return;

        var ids = changedIds?.Select(id => (long)id).ToArray();
        var rows = await db.RoleMails
            .Where(m => m.RoleId == roleId && (ids == null || ids.Contains(m.MailId)))
            .ToListAsync(cancellationToken);

        mails = changedIds is null ? mails : mails.Where(s => changedIds.Contains(s.MailId)).ToArray();
        var live = mails.ToDictionary(m => m.MailId);

        foreach (var row in rows)
        {
            if (!live.TryGetValue(unchecked((uint)row.MailId), out var entry))
            {
                db.RoleMails.Remove(row);
                continue;
            }

            WriteRow(row, roleId, entry);
        }

        var stored = rows.Select(r => unchecked((uint)r.MailId)).ToHashSet();

        foreach (var entry in mails)
        {
            if (stored.Contains(entry.MailId))
                continue;

            var row = new RoleMail();
            WriteRow(row, roleId, entry);
            db.RoleMails.Add(row);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogDebug("role {RoleId} mails saved ({Count} mails)", roleId, mails.Count);
    }

    private static void WriteRow(RoleMail row, long roleId, MailEntry entry)
    {
        row.RoleId = roleId;
        row.MailId = unchecked(entry.MailId);
        row.TemplateId = entry.TemplateId;
        row.Type = entry.Type;
        row.Important = entry.Important;
        row.Open = entry.Open;
        row.HasAttach = entry.HasUnclaimed;
        row.Items = JsonSerializer.Serialize(entry.Items, SaveJson.Options);
        row.TemplateContentParams = JsonSerializer.Serialize(entry.TemplateContentParams, SaveJson.Options);
        row.Contents = JsonSerializer.Serialize(entry.Contents, SaveJson.Options);
        row.Time = unchecked(entry.Time);
        row.ExpireTime = unchecked(entry.ExpireTime);
    }

    private static IReadOnlyList<T> ParseText<T>(string json, long mailId, long roleId)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, SaveJson.Options)
                ?? throw new InvalidDataException($"Mail {mailId} for role {roleId} has invalid text.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Mail {mailId} for role {roleId} has corrupt text.", ex);
        }
    }

    /// <summary>Reject corrupt attachments so the next save cannot erase them.</summary>
    private IReadOnlyList<ItemGrant> ParseItems(RoleMail row, long roleId)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<ItemGrant>>(row.Items, SaveJson.Options);

            return items?.Where(grant => grant.ItemId != 0 && grant.Count != 0).ToList()
                   ?? throw new InvalidDataException($"Mail {row.MailId} for role {roleId} has invalid attachments.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex, "role {RoleId} mail {MailId} items failed to parse",
                roleId, row.MailId);
            throw new InvalidDataException($"Mail {row.MailId} for role {roleId} has corrupt attachments.", ex);
        }
    }
}
