using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleShopGoods(ILogger<HandleShopGoods> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsShopGoods)]
    public Task<SCShopGoods> OnPacket(NetContext ctx, CSShopGoods req)
    {
        if (req.ShopId <= 0 || !ctx.Player.Shop.ShopExists((uint)req.ShopId))
            return Task.FromResult(new SCShopGoods {
                Result = (int)EnmTextCode.EnmTextShopNotExsit,
                ShopId = req.ShopId
            });

        var res = new SCShopGoods { Result = 0, ShopId = req.ShopId };
        res.Goods.AddRange(ctx.Player.Shop.GoodsInfo((uint)req.ShopId));
        logger.LogDebug("shop {ShopId} goods: {Count} listed", req.ShopId, res.Goods.Count);
        return Task.FromResult(res);
    }
}
