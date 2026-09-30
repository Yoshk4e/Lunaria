using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSigninActivityReward
{
    [GameHandler(EClientServerCmds.CsSigninActivityReward)]
    public async Task<SCSignInActivityReward> OnPacket(NetContext ctx, CSSignInActivityReward req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCSignInActivityReward {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                ActivityId = req.ActivityId,
                Day = req.Day
            };

        var (result, items, delivery) = ctx.Player.ClaimSignInReward(req.ActivityId, req.Day);
        if (result == 0)
            await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var reply = new SCSignInActivityReward {
            Result = result,
            ActivityId = req.ActivityId,
            Day = req.Day
        };

        reply.RewardItems.AddRange(items.Select(item => new SignInRewardItem {
            ItemId = item.ItemId,
            ItemCount = item.Count
        }));
        return reply;
    }
}
