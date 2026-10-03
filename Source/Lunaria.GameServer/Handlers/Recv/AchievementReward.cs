using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleAchievementReward
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsAchievementReward)]
    public async Task<SCAchievementReward> OnPacket(NetContext ctx, CSAchievementReward req)
    {
        var ids = req.AllIds.Where(id => id > 0).Select(id => (uint)id).ToList();

        var (code, delivery) = ctx.Player.ClaimAchievementRewards(ids);
        if (code != 0)
            return new SCAchievementReward { Result = code, AllIds = { req.AllIds } };
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        return new SCAchievementReward { Result = 0, AllIds = { req.AllIds } };
    }
}
