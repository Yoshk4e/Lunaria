using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRedPointList
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqRedpointList)]
    public Task<SCRedPointList> OnPacket(NetContext ctx, CSRedPointList req)
    {
        return Task.FromResult(new SCRedPointList {
            Result = 0,
            RoleExchangeActivity = ctx.Player.RedPoints.ExchangeActivity
        });
    }
}
