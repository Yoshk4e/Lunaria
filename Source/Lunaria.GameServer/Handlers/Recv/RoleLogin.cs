using Lunaria.GameServer.Net;
using Lunaria.GameServer.Services;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRoleLogin(RoleSessionService sessions)
{
    [GameHandler(EClientServerCmds.CsRoleLogin)]
    public async Task<SCRoleLogin> OnPacket(NetContext ctx, CSRoleLogin req)
    {
        var code = await sessions.ActivateAsync(ctx, req.RoleId).ConfigureAwait(false);
        return new SCRoleLogin {
            Result = code,
            MapId = code == 0 ? ctx.Player.Map.SpawnMap : 0,
            CurrentSavepoint = code == 0 ? ctx.Player.Map.Savepoint : 0,
            TodayFirstLogin = false
        };
    }
}
