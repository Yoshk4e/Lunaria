using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMapCancelTrackTarget
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMapCancelTrackTarget)]
    public Task<SCMapCancelTrackTarget> OnPacket(NetContext ctx, CSMapCancelTrackTarget req)
    {
        return Task.FromResult(new SCMapCancelTrackTarget {
            Result = ctx.Player.Map.CancelTrackTarget(req.MapId, req.TagId, req.TagType)
        });
    }
}
