using Lunaria.Game.Logging;
using Lunaria.Game.Player.Persistence.Saves;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

/// <summary>Save changed rows and sections in one transaction. Accept the saved batch after commit.</summary>
public sealed class RoleStateStore(
    IDbContextFactory<GameDbContext> factory,
    RoleRepository roles,
    CharacterRepository characters,
    MotiveRepository motives,
    GuideRepository guides,
    MailRepository mails,
    RoleSaveRepository saves)
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Persistence");

    public async Task<IReadOnlyList<Msg.CharacterData>> PreviewCharactersAsync(Player session, long roleId,
        CancellationToken cancellationToken = default)
    {
        using var logScope = Log.BeginPlayerScope(session.SessionId, roleId);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.Roles.AnyAsync(r => r.Id == roleId && r.AccountId == session.Account.Id, cancellationToken))
            throw new InvalidOperationException("Role does not belong to this account.");
        var (roster, _) = await characters.LoadAsync(db, roleId, cancellationToken);
        var preview = session.CreateRoleSession();
        preview.Characters.Load(roster);
        await transaction.CommitAsync(cancellationToken);
        return preview.Characters.ListData();
    }

    public async Task SaveAsync(Player player, CancellationToken cancellationToken = default)
    {
        using var logScope = Log.BeginPlayerScope(player.SessionId, player.Roles.Active()?.Id);
        using var operationTime = player.BeginOperation();
        if (!player.IsLoggedIn || !player.IsDirty) return;

        var changedRoles = player.Roles.DirtyRoles();
        var active = player.Roles.Active();
        if (active is null && changedRoles.Count == 0) return;
        var characterIds = player.Characters.ChangedIds;
        var motiveIds = player.Motives.ChangedIds;
        var mailIds = player.Mails.ChangedIds;
        var roster = characterIds.Select(player.Characters.Get).OfType<Lunaria.Game.Characters.CharacterState>().ToArray();
        var motiveRows = motiveIds.Select(player.Motives.Get).OfType<Lunaria.Game.Motives.MotiveState>().ToArray();
        var mailRows = mailIds.Count == 0 ? [] : player.Mails.Entries.Where(m => mailIds.Contains(m.MailId)).ToArray();
        var guideEntries = player.Guides.IsDirty ? player.Guides.Entries.ToDictionary(p => p.Key, p => p.Value) : null;
        var counterChanged = player.Guid.IsDirty;
        var counter = player.Guid.LastMinted;
        var sections = active is null ? [] : player.SaveBaseline.CaptureChanges(player);
        var batch = active is null ? player.Roles.Changes.Capture() : player.Changes.Capture();
        if (changedRoles.Count == 0 && characterIds.Count == 0 && motiveIds.Count == 0 && mailIds.Count == 0
            && !counterChanged && guideEntries is null && sections.Count == 0)
        {
            batch.Accept();
            return;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var role in changedRoles)
        {
            if (!await db.Roles.AnyAsync(r => r.Id == role.Id && r.AccountId == player.Account.Id, cancellationToken))
                throw new InvalidOperationException("Cannot save a role outside its account.");
            await roles.SaveAsync(db, role, cancellationToken);
        }
        if (active is not null)
        {
            var row = await db.Roles.SingleOrDefaultAsync(r => r.Id == active.Id && r.AccountId == player.Account.Id, cancellationToken)
                ?? throw new InvalidOperationException("Cannot save a role outside its account.");
            if (counterChanged) row.LastMintedInstId = (long)counter;
            if (characterIds.Count > 0) await characters.SaveAsync(db, active.Id, roster, counter, cancellationToken, characterIds);
            if (motiveIds.Count > 0) await motives.SaveAsync(db, active.Id, motiveRows, cancellationToken, motiveIds);
            if (mailIds.Count > 0) await mails.SaveAsync(db, active.Id, mailRows, cancellationToken, mailIds);
            if (guideEntries is not null) await guides.SaveEntriesAsync(db, active.Id, guideEntries, cancellationToken);
            await saves.WriteAsync(db, active.Id, sections, player.UtcNow.UtcDateTime, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        if (active is not null) player.SaveBaseline.Accept(sections);
        batch.Accept();
        Log.Event("role changes committed: {Roles} roles, {Characters} characters, {Motives} motives, {Mails} mails, {Sections} sections",
            changedRoles.Count, characterIds.Count, motiveIds.Count, mailIds.Count, sections.Count);
    }

    public async Task<Player> LoadAsync(Player session, long roleId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        using var logScope = Log.BeginPlayerScope(session.SessionId, roleId);
        using var operationTime = session.BeginOperation(now);
        var next = session.CreateRoleSession();
        if (!next.Roles.SetActive(roleId)) throw new InvalidOperationException("Unknown role.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.Roles.AnyAsync(r => r.Id == roleId && r.AccountId == session.Account.Id, cancellationToken))
            throw new InvalidOperationException("Role does not belong to this account.");
        var (roster, lastMinted) = await characters.LoadAsync(db, roleId, cancellationToken);
        next.Characters.Load(roster);
        next.Guid.Adopt(Math.Max(lastMinted, roster.Select(c => c.InstId).DefaultIfEmpty().Max()));
        next.Motives.Load(await motives.LoadAsync(db, roleId, cancellationToken));
        var hasSave = await saves.LoadIntoAsync(db, roleId, next, cancellationToken);
        next.Guides.Load(await guides.LoadAsync(db, roleId, cancellationToken));
        next.Mails.Load(await mails.LoadAsync(db, roleId, cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        next.Changes.AcceptAll();
        if (!next.SaveBaseline.HasSections || next.SaveBaseline.Repairs.Count > 0)
            next.Changes.Invalidate("save format or hydration repair");
        next.InitializeRoleState(now, hasSave);
        Log.State("role {RoleId} loaded, save {HasSave}", roleId, hasSave);
        return next;
    }
}
