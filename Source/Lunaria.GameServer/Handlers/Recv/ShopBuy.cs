using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleShopBuy(ILogger<HandleShopBuy> logger)
{
    [GameHandler(EClientServerCmds.CsShopBuy)]
    public async Task<SCShopBuy> OnPacket(NetContext ctx, CSShopBuy req)
    {
        SCShopBuy Reject(int code)
        {
            return new SCShopBuy { Result = code, ShopId = req.ShopId };
        }

        if (!ctx.Player.HasActiveRole)
            return Reject((int)EnmTextCode.EnmTextNotAccLogin);

        if (req.ShopId <= 0)
            return Reject((int)EnmTextCode.EnmTextShopNotExsit);

        var shopId = (uint)req.ShopId;
        var basket = req.Goods.Select(good => (good.Id, good.Num)).ToList();

        var now = ctx.Player.UtcNow;
        var (code, delivery) = ctx.Player.BuyFromShop(shopId, basket, now);

        if (code != 0 || delivery is null)
            return Reject(code);

        await ctx.NotifyAsync(delivery.Presentation)
            .ConfigureAwait(false);

        var res = new SCShopBuy { Result = 0, ShopId = req.ShopId };
        res.Goods.AddRange(req.Goods);
        logger.LogDebug("shop {ShopId} buy: {Count} lines", req.ShopId, req.Goods.Count);
        return res;
    }
}
