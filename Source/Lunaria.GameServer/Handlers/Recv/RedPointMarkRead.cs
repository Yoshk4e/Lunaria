using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRedPointMarkRead
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqRedpointMarkread)]
    public Task<SCRedPointMarkRead> OnPacket(NetContext ctx, CSRedPointMarkRead req)
    {
        ctx.Player.RedPoints.MarkExchangeActivityRead();

        return Task.FromResult(new SCRedPointMarkRead {
            Result = 0,
            RoleExchangeActivity = ctx.Player.RedPoints.ExchangeActivity
        });
    }
}
