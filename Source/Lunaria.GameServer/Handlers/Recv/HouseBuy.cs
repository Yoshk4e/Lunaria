using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleHouseBuy
{
    [GameHandler(EClientServerCmds.CsReqBuyHouse)]
    public async Task<SCResBuyHouse> OnPacket(NetContext ctx, CSReqBuyHouse req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCResBuyHouse { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var (code, info) = ctx.Player.BuyHouse(req.HouseId);

        if (code != 0)
            return new SCResBuyHouse { Result = (uint)code };

        if (info is not null)
        {
        }

        return new SCResBuyHouse { Result = 0, HouseInfo = info };
    }
}
