using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMailGetList
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMailGetList)]
    public Task<SCMailGetList> OnPacket(NetContext ctx, CSMailGetList req)
    {
        var reply = new SCMailGetList {
            FromMailId = req.FromMailId,
            Count = req.Count
        };

        reply.Mails.AddRange(ctx.Player.Mails.ListData(req.FromMailId, req.Count));

        return Task.FromResult(reply);
    }
}
