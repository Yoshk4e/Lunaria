using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMailDelAllReaded(ILogger<HandleMailDelAllReaded> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMailDelAllReaded)]
    public Task<SCMailDelAllReaded> OnPacket(NetContext ctx, CSMailDelAllReaded req)
    {
        var removed = ctx.Player.Mails.DeleteAllRead();

        if (removed.Count > 0)
            logger.LogDebug("mails deleted all read: {Count} mails", removed.Count);

        return Task.FromResult(new SCMailDelAllReaded {
            Result = 0
        });
    }
}
