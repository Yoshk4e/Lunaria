using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleHouseIncome
{
    [GameHandler(EClientServerCmds.CsReqClaimHouseIncome)]
    public async Task<SCResClaimHouseIncome> OnPacket(NetContext ctx, CSReqClaimHouseIncome req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCResClaimHouseIncome { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var delivery = ctx.Player.ClaimHouseIncome();
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var reply = new SCResClaimHouseIncome { Result = 0 };
        reply.HouseInfoList.AddRange(ctx.Player.Houses.ToHouseInfoList(ctx.Player.UtcNow));
        return reply;
    }
}
