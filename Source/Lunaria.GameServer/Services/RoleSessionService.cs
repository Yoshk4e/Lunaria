using Lunaria.Game.Player.Persistence;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Services;

public sealed class RoleSessionService(RoleStateStore store, ILogger<RoleSessionService> logger)
{
    public async Task<SCCharacterListRsp> CharacterListAsync(NetContext ctx)
    {
        if (!ctx.Player.IsLoggedIn) return new SCCharacterListRsp { Result = (int)EnmTextCode.EnmTextNotAccLogin };
        if (ctx.Player.HasActiveRole) return new SCCharacterListRsp { List = { ctx.Player.Characters.ListData() } };
        if (ctx.Player.Roles.Newest() is not {} newest) return new SCCharacterListRsp();
        try
        {
            return new SCCharacterListRsp { List = { await store.PreviewCharactersAsync(ctx.Player, newest.Id).ConfigureAwait(false) } };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "role {RoleId} roster preview failed", newest.Id);
            return new SCCharacterListRsp { Result = (int)EnmTextCode.EnmTextAccLoginAccountDataFail };
        }
    }

    public async Task<int> ActivateAsync(NetContext ctx, ulong requestedRole)
    {
        if (!ctx.Player.IsLoggedIn) return (int)EnmTextCode.EnmTextNotAccLogin;
        if (requestedRole > long.MaxValue || ctx.Player.Roles.Get((long)requestedRole) is null)
            return (int)EnmTextCode.EnmTextAccLoginAccountDataFail;
        try
        {
            await store.SaveAsync(ctx.Player).ConfigureAwait(false);
            var next = await store.LoadAsync(ctx.Player, (long)requestedRole, ctx.Player.UtcNow).ConfigureAwait(false);
            ctx.ReplacePlayer(next);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "role {RoleId} activation failed; keeping current role", requestedRole);
            return (int)EnmTextCode.EnmTextAccLoginAccountDataFail;
        }
    }

    public async Task<int> LogoutAsync(NetContext ctx, ulong requestedRole)
    {
        if (!ctx.Player.HasActiveRole) return (int)EnmTextCode.EnmTextNotAccLogin;
        if ((ulong)ctx.Player.Roles.Active()!.Id != requestedRole) return (int)EnmTextCode.EnmTextWrongParam;
        try
        {
            await store.SaveAsync(ctx.Player).ConfigureAwait(false);
            ctx.ReplacePlayer(ctx.Player.CreateRoleSession());
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "role {RoleId} logout save failed", requestedRole);
            return (int)EnmTextCode.EnmTextAccLoginAccountDataFail;
        }
    }
}
