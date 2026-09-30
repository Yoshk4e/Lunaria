using Lunaria.Game.Logging;
using Lunaria.Game.Player.Persistence.Saves;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Player.Persistence;

/// <summary>Save one role snapshot per transaction. Clear dirty flags only after commit.</summary>
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
        if (!player.IsLoggedIn || !player.IsDirty) return;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var changedRoles = player.Roles.DirtyRoles();
        foreach (var role in changedRoles)
        {
            if (!await db.Roles.AnyAsync(r => r.Id == role.Id && r.AccountId == player.Account.Id, cancellationToken))
                throw new InvalidOperationException("Cannot save a role outside its account.");
            await roles.SaveAsync(db, role, cancellationToken);
        }
        Log.Event("saving {Roles} dirty role rows and the active role tables", changedRoles.Count);

        if (player.Roles.Active() is {} active)
        {
            if (!await db.Roles.AnyAsync(r => r.Id == active.Id && r.AccountId == player.Account.Id, cancellationToken))
                throw new InvalidOperationException("Cannot save a role outside its account.");
            if (player.Characters.IsDirty || player.Motives.IsDirty || player.Collections.IsDirty || player.Mails.IsDirty || player.TemporaryTeamDirty)
                await characters.SaveAsync(db, active.Id, player.Characters.All, player.Guid.LastMinted, cancellationToken);
            if (player.Motives.IsDirty) await motives.SaveAsync(db, active.Id, player.Motives.All, cancellationToken);
            if (player.Mails.IsDirty) await mails.SaveAsync(db, active.Id, player.Mails.Entries, cancellationToken);
            if (player.Guides.IsDirty) await guides.SaveAsync(db, active.Id, player.Guides, cancellationToken);
            // Vitals are saved separately but use the roster's dirty flag. Damage and healing must save both.
            if (player.SaveDirty || player.Characters.IsDirty)
                await saves.SaveAsync(db, active.Id, player, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        foreach (var role in changedRoles) player.Roles.MarkPersisted(role.Id);
        if (!player.HasActiveRole) return;
        Log.Event("role {RoleId} save committed", player.Roles.Active()!.Id);
        player.Characters.ClearDirty();
        player.Motives.ClearDirty();
        player.Mails.ClearDirty();
        player.Guides.ClearDirty();
        player.ClearSaveDirty();
    }

    public async Task<Player> LoadAsync(Player session, long roleId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
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
        next.InitializeRoleState(now, hasSave);
        Log.State("role {RoleId} loaded, save {HasSave}", roleId, hasSave);
        return next;
    }
}
