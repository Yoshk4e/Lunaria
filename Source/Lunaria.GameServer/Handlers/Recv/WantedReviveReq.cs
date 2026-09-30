using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedReviveReq
{
    [GameHandler(EClientServerCmds.CsWantedReviveReq)]
    public async Task<SCWantedReviveRes> OnPacket(NetContext ctx, CSWantedReviveReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCWantedReviveRes { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var code = ctx.Player.BuyWantedRevive(req.CharacterUids);

        if (code != 0)
            return new SCWantedReviveRes { Result = code, ReviveCount = ctx.Player.Wanted.ReviveCount };


        return new SCWantedReviveRes {
            Result = 0,
            ReviveCount = ctx.Player.Wanted.ReviveCount
        };
    }
}
