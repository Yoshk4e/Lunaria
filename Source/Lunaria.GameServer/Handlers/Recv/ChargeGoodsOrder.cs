using Google.Protobuf;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleChargeGoodsOrder
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsChaegrGoodsOrder)]
    public async Task<SCChargeGoodsOrder> OnPacket(NetContext ctx, CSChargeGoodsOrder req)
    {
        var outcome = ctx.Player.BuyChargeGoods(req.GoodsId);

        if (outcome.Code != 0)
            return new SCChargeGoodsOrder { Result = outcome.Code, GoodsId = req.GoodsId };

        if (outcome.Delivery is not null)
        {
            await ctx.NotifyAsync(outcome.Delivery.Presentation)
                .ConfigureAwait(false);
        }

        return new SCChargeGoodsOrder {
            Result = 0,
            GoodsId = req.GoodsId,
            Currency = ByteString.CopyFromUtf8(outcome.Currency),
            Price = ByteString.CopyFromUtf8(outcome.Price.ToString())
        };
    }
}
