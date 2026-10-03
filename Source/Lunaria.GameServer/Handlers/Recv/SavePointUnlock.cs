using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSavePointUnlock
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsSavepointUnlock)]
    public async Task<SCSavePointUnlock> OnPacket(NetContext ctx, CSSavePointUnlock req)
    {
        var code = ctx.Player.UnlockSavepoint(req.SavepointId);

        return new SCSavePointUnlock {
            Result = code,
            SavepointId = req.SavepointId
        };
    }
}
