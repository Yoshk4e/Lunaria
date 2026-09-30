using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRedPointList
{
    [GameHandler(EClientServerCmds.CsReqRedpointList)]
    public Task<SCRedPointList> OnPacket(NetContext ctx, CSRedPointList req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCRedPointList { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(new SCRedPointList {
            Result = 0,
            RoleExchangeActivity = ctx.Player.RedPoints.ExchangeActivity
        });
    }
}
