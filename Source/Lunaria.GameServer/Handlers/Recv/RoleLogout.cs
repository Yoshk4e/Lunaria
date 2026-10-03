using Lunaria.GameServer.Net;
using Lunaria.GameServer.Services;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRoleLogout(RoleSessionService sessions)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqRoleLogout)]
    public async Task<SCRoleLogout> OnPacket(NetContext ctx, CSRoleLogout req) => new() {
        Result = await sessions.LogoutAsync(ctx, req.RoleId).ConfigureAwait(false),
        RoleId = req.RoleId
    };
}
