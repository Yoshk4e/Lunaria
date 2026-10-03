using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleHouseInfo
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqHouseInfo)]
    public Task<SCResHouseInfo> OnPacket(NetContext ctx, CSReqHouseInfo req)
    {
        var reply = new SCResHouseInfo { Result = 0 };
        reply.HouseList.AddRange(ctx.Player.Houses.ToHouseInfoList(ctx.Player.UtcNow));
        return Task.FromResult(reply);
    }
}
