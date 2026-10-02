using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCaseCluePut
{
    [GameHandler(EClientServerCmds.CsCaseCluePut)]
    public Task<SCCaseCluePut> OnPacket(NetContext ctx, CSCaseCluePut req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCCaseCluePut {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                ClueId = req.ClueId
            });

        var (result, caseId, phase) = ctx.Player.Cases.PutClue(req.ClueId);

        return Task.FromResult(new SCCaseCluePut {
            Result = result,
            ClueId = req.ClueId,
            CaseId = caseId,
            FinishedPhase = ctx.Player.Cases.FinishedStageId(caseId, phase)
        });
    }
}
