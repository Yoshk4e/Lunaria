using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedBionicsAttribQuery
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedBionicsAttribQuery)]
    public Task<SCWantedBionicsAttribQuery> OnPacket(NetContext ctx, CSWantedBionicsAttribQuery req)
    {
        var reply = new SCWantedBionicsAttribQuery { Result = 0 };
        reply.Data.AddRange(ctx.Player.Wanted.BionicsAttribData(req.InstId, ctx.Player.Progress.WorldLevel));
        return Task.FromResult(reply);
    }
}
