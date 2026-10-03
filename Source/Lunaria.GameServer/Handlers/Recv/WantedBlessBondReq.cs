using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedBlessBondReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedBlessBondReq)]
    public Task<SCWantedBlessBondRes> OnPacket(NetContext ctx, CSWantedBlessBondReq req)
    {
        var resource = ctx.Player.Wanted.ToResource();
        var reply = new SCWantedBlessBondRes { Result = 0 };
        reply.BlessIds.AddRange(resource.BlessIds);
        reply.Bonds.AddRange(resource.Bonds);
        return Task.FromResult(reply);
    }
}
