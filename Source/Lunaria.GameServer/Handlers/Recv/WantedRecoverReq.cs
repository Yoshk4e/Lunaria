using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedRecoverReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedRecoverReq)]
    public async Task<SCWantedRecoverRes> OnPacket(NetContext ctx, CSWantedRecoverReq req)
    {
        if (!ctx.Player.WantedRecover())
            return new SCWantedRecoverRes { Result = (int)EnmTextCode.EnmTextWantedNotInWanted };


        return new SCWantedRecoverRes { Result = 0 };
    }
}
