using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedBionicsReq
{
    [GameHandler(EClientServerCmds.CsWantedBionicsReq)]
    public Task<SCWantedBionicsRes> OnPacket(NetContext ctx, CSWantedBionicsReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedBionicsRes { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var resource = ctx.Player.Wanted.ToResource();
        var reply = new SCWantedBionicsRes { Result = 0 };
        reply.Bionics.AddRange(resource.Bionics);
        return Task.FromResult(reply);
    }
}
