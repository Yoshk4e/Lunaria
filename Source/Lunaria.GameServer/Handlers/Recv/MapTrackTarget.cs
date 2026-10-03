using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMapTrackTarget
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMapTrackTarget)]
    public Task<SCMapTrackTarget> OnPacket(NetContext ctx, CSMapTrackTarget req)
    {
        return Task.FromResult(new SCMapTrackTarget {
            Result = ctx.Player.Map.TrackTarget(req.MapId, req.TagId, req.TagType)
        });
    }
}
