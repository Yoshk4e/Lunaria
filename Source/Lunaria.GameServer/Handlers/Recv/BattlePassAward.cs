using Lunaria.Game.Resources;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleBattlePassAward
{
    [GameHandler(EClientServerCmds.CsBattlePassAward)]
    public async Task<SCBattlePassAward> OnPacket(NetContext ctx, CSBattlePassAward req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCBattlePassAward { Result = (int)EnmTextCode.EnmTextNotAccLogin, Id = req.Id };

        var (code, highest, grants, delivery) = ctx.Player.ClaimBattlePassAwards(req.Id);
        if (code != 0)
            return new SCBattlePassAward { Result = code, Id = req.Id };
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);
        var reply = new SCBattlePassAward { Result = 0, Id = req.Id, Level = highest };
        reply.Items.AddRange(ItemsOf(grants));
        return reply;
    }

    private static IEnumerable<CmdItem> ItemsOf(IReadOnlyList<ItemGrant> grants) =>
        grants.Select(grant => new CmdItem { ItemId = grant.ItemId, ItemNum = grant.Count });
}
