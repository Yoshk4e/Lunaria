using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedRelicsReq
{
    [GameHandler(EClientServerCmds.CsWantedRelicsReq)]
    public Task<SCWantedRelicsRes> OnPacket(NetContext ctx, CSWantedRelicsReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedRelicsRes { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var resource = ctx.Player.Wanted.ToResource();
        var reply = new SCWantedRelicsRes { Result = 0 };
        reply.RelicsIds.AddRange(resource.RelicsIds);
        return Task.FromResult(reply);
    }
}
