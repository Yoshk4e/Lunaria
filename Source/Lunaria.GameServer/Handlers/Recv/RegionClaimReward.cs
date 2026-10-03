using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleRegionClaimReward
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqClaimReward)]
    public async Task<SCResClaimReward> OnPacket(NetContext ctx, CSReqClaimReward req)
    {
        var (code, values, delivery) = ctx.Player.ClaimRegionRewards(req.SubRegionId);
        if (code != 0)
            return new SCResClaimReward { Result = (uint)code, SubRegionId = req.SubRegionId };
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var reply = new SCResClaimReward { Result = 0, SubRegionId = req.SubRegionId };
        reply.ProgressValues.AddRange(values.Select(value => (EnmProgressValue)value));
        return reply;
    }
}
