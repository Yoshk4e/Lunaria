using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleTalentUnlock
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsTalentUnlock)]
    public Task<SCTalentUnlock> OnPacket(NetContext ctx, CSTalentUnlock req)
    {
        var code = ctx.Player.UnlockTalent(req.InstId, req.TalentNode);

        return Task.FromResult(new SCTalentUnlock {
            Result = code,
            InstId = req.InstId,
            TalentNode = req.TalentNode
        });
    }
}
