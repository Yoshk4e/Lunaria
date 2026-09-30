using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMapTrackTarget
{
    [GameHandler(EClientServerCmds.CsMapTrackTarget)]
    public Task<SCMapTrackTarget> OnPacket(NetContext ctx, CSMapTrackTarget req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCMapTrackTarget { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(new SCMapTrackTarget {
            Result = ctx.Player.Map.TrackTarget(req.MapId, req.TagId, req.TagType)
        });
    }
}
