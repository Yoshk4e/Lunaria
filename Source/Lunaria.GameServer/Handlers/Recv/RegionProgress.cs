using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleRegionProgress
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqRegionProgress)]
    public Task<SCResRegionProgress> OnPacket(NetContext ctx, CSReqRegionProgress req)
    {
        ctx.Player.RecalculateRegionProgress();
        return Task.FromResult(ctx.Player.RegionProgress.ToRegionProgress());
    }
}
