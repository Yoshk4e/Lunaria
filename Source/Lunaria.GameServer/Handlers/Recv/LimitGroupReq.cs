using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleLimitGroupReq(ILogger<HandleLimitGroupReq> logger)
{
    [GameHandler(EClientServerCmds.CsLimitGroupReq)]
    public async Task<SCLimitGroupRes> OnPacket(NetContext ctx, CSLimitGroupReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCLimitGroupRes { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var now = ctx.Player.UtcNow;
        var reset = ctx.Player.Limits.Refresh(now);
        var groups = ctx.Player.Limits.Groups(now);
        logger.LogDebug("limit groups: {Count} tracked, {Reset} reset", groups.Count, reset);

        if (reset > 0)
        {
            var ntf = new SCLimitGroupNtf();
            ntf.Groups.AddRange(groups);

            try
            {
                await ctx.NotifyAsync(ntf).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                logger.LogDebug(ex, "limit group reset notification dropped");
            }
        }

        var res = new SCLimitGroupRes { Result = 0 };
        res.Groups.AddRange(groups);
        return res;
    }
}
