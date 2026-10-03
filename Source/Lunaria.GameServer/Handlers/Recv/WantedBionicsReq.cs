using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedBionicsReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedBionicsReq)]
    public Task<SCWantedBionicsRes> OnPacket(NetContext ctx, CSWantedBionicsReq req)
    {
        var resource = ctx.Player.Wanted.ToResource();
        var reply = new SCWantedBionicsRes { Result = 0 };
        reply.Bionics.AddRange(resource.Bionics);
        return Task.FromResult(reply);
    }
}
