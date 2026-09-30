using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveRefineUp(ILogger<HandleMotiveRefineUp> logger)
{
    [GameHandler(EClientServerCmds.CsMotiveRefineup)]
    public Task<SCMotiveRefineUp> OnPacket(NetContext ctx, CSMotiveRefineUp req)
    {
        SCMotiveRefineUp Reject(int code, uint refine = 0)
        {
            return new SCMotiveRefineUp {
                Result = code,
                MotiveUniqId = req.MotiveUniqId,
                OldRefine = refine,
                CurrentRefine = refine
            };
        }

        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(Reject((int)EnmTextCode.EnmTextNotAccLogin));

        var outcome = ctx.Player.RefineMotive(req.MotiveUniqId, req.RefineMotiveUniqIds.ToList());

        if (!outcome.Ok)
            return Task.FromResult(Reject(outcome.Code, outcome.OldRefine));

        logger.LogDebug(
            "motive {Motive} refine {Old} -> {New}",
            req.MotiveUniqId, outcome.OldRefine, outcome.NewRefine);

        return Task.FromResult(new SCMotiveRefineUp {
            Result = 0,
            MotiveUniqId = req.MotiveUniqId,
            OldRefine = outcome.OldRefine,
            CurrentRefine = outcome.NewRefine
        });
    }
}
