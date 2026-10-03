using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedShopReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedShopReq)]
    public Task<SCWantedShopRes> OnPacket(NetContext ctx, CSWantedShopReq req)
    {
        return Task.FromResult(new SCWantedShopRes {
            Result = 0,
            Shop = ctx.Player.Wanted.ToShop(req.ShopId)
        });
    }
}
