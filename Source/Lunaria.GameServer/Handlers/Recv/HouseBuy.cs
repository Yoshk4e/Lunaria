using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleHouseBuy
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqBuyHouse)]
    public Task<SCResBuyHouse> OnPacket(NetContext ctx, CSReqBuyHouse req)
    {
        var (code, info) = ctx.Player.BuyHouse(req.HouseId);

        if (code != 0)
            return Task.FromResult(new SCResBuyHouse { Result = (uint)code });

        return Task.FromResult(new SCResBuyHouse { Result = 0, HouseInfo = info });
    }
}
