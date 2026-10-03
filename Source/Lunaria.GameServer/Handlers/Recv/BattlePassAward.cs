using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleBattlePassAward
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsBattlePassAward)]
    public async Task<SCBattlePassAward> OnPacket(NetContext ctx, CSBattlePassAward req)
    {
        var (code, highest, grants, delivery) = ctx.Player.ClaimBattlePassAwards(req.Id);
        if (code != 0)
            return new SCBattlePassAward { Result = code, Id = req.Id };
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);
        var reply = new SCBattlePassAward { Result = 0, Id = req.Id, Level = highest };
        reply.Items.AddRange(ctx.Player.RewardItems(grants));
        return reply;
    }
}
