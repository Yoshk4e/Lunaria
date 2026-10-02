using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGetPoolInfo
{
    [GameHandler(EClientServerCmds.CsGetPoolInfoReq)]
    public Task<SCGetPoolInfo> OnPacket(NetContext ctx, CSGetPoolInfo req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCGetPoolInfo { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var res = new SCGetPoolInfo { Result = 0 };
        res.PoolInfo.AddRange(ctx.Player.GetPoolInfo(req.PoolId, ctx.Player.UtcNow));
        return Task.FromResult(res);
    }
}
