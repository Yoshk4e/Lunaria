using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRoleInfoReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsRoleInfoReq)]
    public Task<SCRoleInfoRes> OnPacket(NetContext ctx, CSRoleInfoReq req) => Task.FromResult(new SCRoleInfoRes {
        RoleInfo = ctx.Player.Roles.Active() is {} role ? ctx.Player.RoleInfo(role) : null
    });
}
