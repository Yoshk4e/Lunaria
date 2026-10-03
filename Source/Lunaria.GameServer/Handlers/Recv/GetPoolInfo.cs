using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGetPoolInfo
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsGetPoolInfoReq)]
    public Task<SCGetPoolInfo> OnPacket(NetContext ctx, CSGetPoolInfo req)
    {
        var res = new SCGetPoolInfo { Result = 0 };
        res.PoolInfo.AddRange(ctx.Player.GetPoolInfo(req.PoolId, ctx.Player.UtcNow));
        return Task.FromResult(res);
    }
}
