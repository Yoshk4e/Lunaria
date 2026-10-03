using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedResetPoint
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedResetPoint)]
    public Task<SCWantedResetPoint> OnPacket(NetContext ctx, CSWantedResetPoint req)
    {
        if (req.Location is {} location)
            ctx.Player.Wanted.SetResetPoint(req.Id, (location.X, location.Y, location.Z));

        return Task.FromResult(new SCWantedResetPoint { Id = req.Id, Result = 0 });
    }
}
