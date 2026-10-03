using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMailRecvAttachments(ILogger<HandleMailRecvAttachments> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMailRecvAttachments)]
    public async Task<SCMailRecvAttachments> OnPacket(NetContext ctx, CSMailRecvAttachments req)
    {
        var (code, delivery) = ctx.Player.ClaimMailAttachments(req.MailId);

        if (code != 0 || delivery is null)
            return new SCMailRecvAttachments {
                Result = code,
                MailId = req.MailId
            };

        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);
        logger.LogDebug("mail {MailId} attachments claimed", req.MailId);

        return new SCMailRecvAttachments {
            Result = 0,
            MailId = req.MailId
        };
    }
}
