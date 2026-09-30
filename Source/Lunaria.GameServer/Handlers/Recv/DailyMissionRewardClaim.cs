using Lunaria.Game.Resources;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDailyMissionRewardClaim
{
    [GameHandler(EClientServerCmds.CsDailyMissionRewardClaim)]
    public async Task<SCDailyMissionRewardClaim> OnPacket(NetContext ctx, CSDailyMissionRewardClaim req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCDailyMissionRewardClaim {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                RewardData = new DailyMissionRewardData()
            };

        var delivery = ctx.Player.ClaimDailyMissionRewards();
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var data = new DailyMissionRewardData();
        data.ClaimedRewardids.AddRange(ctx.Player.DailyMissions.ClaimedRewards);
        return new SCDailyMissionRewardClaim { Result = 0, RewardData = data };
    }
}
