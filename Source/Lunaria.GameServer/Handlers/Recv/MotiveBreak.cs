using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveBreak(ILogger<HandleMotiveBreak> logger)
{
    [GameHandler(EClientServerCmds.CsMotiveBreak)]
    public async Task<SCMotiveBreak> OnPacket(NetContext ctx, CSMotiveBreak req)
    {
        SCMotiveBreak Reject(int code, uint oldBreak = 0)
        {
            return new SCMotiveBreak {
                Result = code,
                MotiveUniqId = req.MotiveUniqId,
                OldBreakLevel = oldBreak,
                NewBreakLevel = oldBreak
            };
        }

        if (!ctx.Player.HasActiveRole)
            return Reject((int)EnmTextCode.EnmTextNotAccLogin);

        var outcome = ctx.Player.BreakMotive(req.MotiveUniqId);

        if (!outcome.Ok)
            return Reject(outcome.Code, outcome.OldBreakLevel);

        logger.LogDebug(
            "motive {Motive} break {Old} -> {New}",
            req.MotiveUniqId, outcome.OldBreakLevel, outcome.NewBreakLevel);

        return new SCMotiveBreak {
            Result = 0,
            MotiveUniqId = req.MotiveUniqId,
            OldBreakLevel = outcome.OldBreakLevel,
            NewBreakLevel = outcome.NewBreakLevel
        };
    }
}
