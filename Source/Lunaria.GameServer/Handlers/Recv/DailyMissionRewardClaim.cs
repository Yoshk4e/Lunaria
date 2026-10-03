using Lunaria.Game.Resources;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDailyMissionRewardClaim
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDailyMissionRewardClaim)]
    public async Task<SCDailyMissionRewardClaim> OnPacket(NetContext ctx, CSDailyMissionRewardClaim req)
    {
        var delivery = ctx.Player.ClaimDailyMissionRewards();
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var data = new DailyMissionRewardData();
        data.ClaimedRewardids.AddRange(ctx.Player.DailyMissions.ClaimedRewards);
        return new SCDailyMissionRewardClaim { Result = 0, RewardData = data };
    }
}
