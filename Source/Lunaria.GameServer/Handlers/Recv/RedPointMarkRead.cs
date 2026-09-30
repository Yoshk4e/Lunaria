using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRedPointMarkRead
{
    [GameHandler(EClientServerCmds.CsReqRedpointMarkread)]
    public Task<SCRedPointMarkRead> OnPacket(NetContext ctx, CSRedPointMarkRead req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCRedPointMarkRead { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        ctx.Player.RedPoints.MarkExchangeActivityRead();

        return Task.FromResult(new SCRedPointMarkRead {
            Result = 0,
            RoleExchangeActivity = ctx.Player.RedPoints.ExchangeActivity
        });
    }
}
