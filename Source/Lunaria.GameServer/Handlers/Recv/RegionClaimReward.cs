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

        // The claim reply only updates the open widget. The client rebuilds the gifts from the sub-region's stored
        // progress_value, which only a sub-region update refreshes, so a claimed gift came back on reopening.
        if (ctx.Player.RegionProgress.ToSubRegionUpdate(req.SubRegionId) is {} update)
            await ctx.NotifyAsync(update).ConfigureAwait(false);

        var reply = new SCResClaimReward { Result = 0, SubRegionId = req.SubRegionId };
        reply.ProgressValues.AddRange(values.Select(value => (EnmProgressValue)value));
        return reply;
    }
}
