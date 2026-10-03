using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCaseEvidenceDecrypted
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsCaseEvidenceDecrypted)]
    public Task<SCCaseEvidenceDecrypted> OnPacket(NetContext ctx, CSCaseEvidenceDecrypted req)
    {
        return Task.FromResult(new SCCaseEvidenceDecrypted {
            Result = ctx.Player.Cases.DecryptEvidence(req.EvidenceId),
            EvidenceId = req.EvidenceId
        });
    }
}
