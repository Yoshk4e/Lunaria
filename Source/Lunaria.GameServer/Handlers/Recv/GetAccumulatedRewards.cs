using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGetAccumulatedRewards
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsGetAccumulatedRewardsReq)]
    public Task<SCGetAccumulatedRewards> OnPacket(NetContext ctx, CSGetAccumulatedRewards req)
    {
        var (code, mask) = ctx.Player.GetAccumulatedRewards(req.PoolId);

        return Task.FromResult(new SCGetAccumulatedRewards {
            Result = code,
            PoolId = req.PoolId,
            ClaimedRewardsMask = mask
        });
    }
}
