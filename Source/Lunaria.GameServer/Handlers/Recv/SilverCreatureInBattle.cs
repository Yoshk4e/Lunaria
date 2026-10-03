using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureInBattle
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqSilverCreatureInBattle)]
    public Task<SCResSilverCreatureInBattleResult> OnPacket(NetContext ctx, CSReqSilverCreatureInBattle req)
    {
        var (result, uniqId) = ctx.Player.SilverCreatures.SetInBattle(req.UniqId);

        return Task.FromResult(new SCResSilverCreatureInBattleResult {
            Result = (uint)result,
            UniqId = uniqId != 0 ? uniqId : req.UniqId
        });
    }
}
