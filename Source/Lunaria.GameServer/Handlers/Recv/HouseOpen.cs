using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleHouseOpen
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqOpenHouse)]
    public Task<SCResOpenHouse> OnPacket(NetContext ctx, CSReqOpenHouse req)
    {
        var (code, info) = ctx.Player.OpenHouse(req.HouseId);
        return Task.FromResult(new SCResOpenHouse { Result = (uint)code, HouseInfo = info });
    }
}
