using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureInBattle
{
    [GameHandler(EClientServerCmds.CsReqSilverCreatureInBattle)]
    public Task<SCResSilverCreatureInBattleResult> OnPacket(NetContext ctx, CSReqSilverCreatureInBattle req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCResSilverCreatureInBattleResult {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                UniqId = req.UniqId
            });

        var (result, uniqId) = ctx.Player.SilverCreatures.SetInBattle(req.UniqId);

        return Task.FromResult(new SCResSilverCreatureInBattleResult {
            Result = (uint)result,
            UniqId = uniqId != 0 ? uniqId : req.UniqId
        });
    }
}
