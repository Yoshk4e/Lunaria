using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedBionicsAttribQuery
{
    [GameHandler(EClientServerCmds.CsWantedBionicsAttribQuery)]
    public Task<SCWantedBionicsAttribQuery> OnPacket(NetContext ctx, CSWantedBionicsAttribQuery req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedBionicsAttribQuery { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var reply = new SCWantedBionicsAttribQuery { Result = 0 };
        reply.Data.AddRange(ctx.Player.Wanted.BionicsAttribData(req.InstId));
        return Task.FromResult(reply);
    }
}
