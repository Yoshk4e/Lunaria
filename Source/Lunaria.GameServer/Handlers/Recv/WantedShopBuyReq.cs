using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleWantedShopBuyReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedShopBuyReq)]
    public async Task<SCWantedShopBuyRes> OnPacket(NetContext ctx, CSWantedShopBuyReq req)
    {
        var (code, goods) = ctx.Player.BuyWantedShopGood(req.ShopId, req.GoodsId, req.BuyCount);

        if (code != 0 || goods is null)
            return new SCWantedShopBuyRes { Result = code, ShopId = req.ShopId };


        return new SCWantedShopBuyRes {
            Result = 0,
            ShopId = req.ShopId,
            Goods = goods
        };
    }
}
