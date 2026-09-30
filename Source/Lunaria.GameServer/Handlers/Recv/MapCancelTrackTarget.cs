using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMapCancelTrackTarget
{
    [GameHandler(EClientServerCmds.CsMapCancelTrackTarget)]
    public Task<SCMapCancelTrackTarget> OnPacket(NetContext ctx, CSMapCancelTrackTarget req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCMapCancelTrackTarget { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(new SCMapCancelTrackTarget {
            Result = ctx.Player.Map.CancelTrackTarget(req.MapId, req.TagId, req.TagType)
        });
    }
}
