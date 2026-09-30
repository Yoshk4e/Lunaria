using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGetAccumulatedRewards
{
    [GameHandler(EClientServerCmds.CsGetAccumulatedRewardsReq)]
    public Task<SCGetAccumulatedRewards> OnPacket(NetContext ctx, CSGetAccumulatedRewards req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCGetAccumulatedRewards { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var (code, mask) = ctx.Player.GetAccumulatedRewards(req.PoolId);

        return Task.FromResult(new SCGetAccumulatedRewards {
            Result = code,
            PoolId = req.PoolId,
            ClaimedRewardsMask = mask
        });
    }
}
