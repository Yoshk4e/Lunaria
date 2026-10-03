using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleMailRead(ILogger<HandleMailRead> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMailRead)]
    public Task<SCMailRead> OnPacket(NetContext ctx, CSMailRead req)
    {
        if (!ctx.Player.Mails.MarkRead(req.MailId))
            return Task.FromResult(new SCMailRead {
                Result = (int)EnmTextCode.EnmTextWrongParam,
                MailId = req.MailId
            });

        logger.LogDebug("mail {MailId} read", req.MailId);

        return Task.FromResult(new SCMailRead {
            Result = 0,
            MailId = req.MailId
        });
    }
}
