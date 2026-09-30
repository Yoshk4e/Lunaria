using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMailDelAllReaded(ILogger<HandleMailDelAllReaded> logger)
{
    [GameHandler(EClientServerCmds.CsMailDelAllReaded)]
    public Task<SCMailDelAllReaded> OnPacket(NetContext ctx, CSMailDelAllReaded req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCMailDelAllReaded {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            });

        var removed = ctx.Player.Mails.DeleteAllRead();

        if (removed.Count > 0)
            logger.LogDebug("mails deleted all read: {Count} mails", removed.Count);

        return Task.FromResult(new SCMailDelAllReaded {
            Result = 0
        });
    }
}
