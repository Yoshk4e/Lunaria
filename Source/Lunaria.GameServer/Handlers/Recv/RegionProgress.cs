using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleRegionProgress
{
    [GameHandler(EClientServerCmds.CsReqRegionProgress)]
    public Task<SCResRegionProgress> OnPacket(NetContext ctx, CSReqRegionProgress req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCResRegionProgress { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        ctx.Player.RecalculateRegionProgress();
        return Task.FromResult(ctx.Player.RegionProgress.ToRegionProgress());
    }
}
