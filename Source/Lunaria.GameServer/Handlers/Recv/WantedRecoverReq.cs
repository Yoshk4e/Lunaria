using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedRecoverReq
{
    [GameHandler(EClientServerCmds.CsWantedRecoverReq)]
    public async Task<SCWantedRecoverRes> OnPacket(NetContext ctx, CSWantedRecoverReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCWantedRecoverRes { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        if (!ctx.Player.WantedRecover())
            return new SCWantedRecoverRes { Result = (int)EnmTextCode.EnmTextWantedNotInWanted };


        return new SCWantedRecoverRes { Result = 0 };
    }
}
