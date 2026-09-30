using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleReqUnlockTeleport
{
    [GameHandler(EClientServerCmds.CsReqUnlockTeleport)]
    public async Task<SCResUnlockTeleport> OnPacket(NetContext ctx, CSReqUnlockTeleport req)
    {
        var reply = new SCResUnlockTeleport {
            Result = (uint)ctx.Player.UnlockTeleport(req.TeleportId),
            TeleportId = req.TeleportId
        };

        return reply;
    }
}
