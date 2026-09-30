using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedShopReq
{
    [GameHandler(EClientServerCmds.CsWantedShopReq)]
    public Task<SCWantedShopRes> OnPacket(NetContext ctx, CSWantedShopReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedShopRes { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(new SCWantedShopRes {
            Result = 0,
            Shop = ctx.Player.Wanted.ToShop(req.ShopId)
        });
    }
}
