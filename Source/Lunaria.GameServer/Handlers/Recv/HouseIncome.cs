using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleHouseIncome
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqClaimHouseIncome)]
    public async Task<SCResClaimHouseIncome> OnPacket(NetContext ctx, CSReqClaimHouseIncome req)
    {
        var delivery = ctx.Player.ClaimHouseIncome();
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var reply = new SCResClaimHouseIncome { Result = 0 };
        reply.HouseInfoList.AddRange(ctx.Player.Houses.ToHouseInfoList(ctx.Player.UtcNow));
        return reply;
    }
}
