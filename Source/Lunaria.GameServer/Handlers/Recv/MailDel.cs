using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleMailDel(ILogger<HandleMailDel> logger)
{
    [GameHandler(EClientServerCmds.CsMailDel)]
    public Task<SCMailDel> OnPacket(NetContext ctx, CSMailDel req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCMailDel {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                MailId = req.MailId
            });

        if (!ctx.Player.Mails.TryDelete(req.MailId))
            return Task.FromResult(new SCMailDel {
                Result = (int)EnmTextCode.EnmTextWrongParam,
                MailId = req.MailId
            });

        logger.LogDebug("mail {MailId} deleted", req.MailId);

        return Task.FromResult(new SCMailDel {
            Result = 0,
            MailId = req.MailId
        });
    }
}
