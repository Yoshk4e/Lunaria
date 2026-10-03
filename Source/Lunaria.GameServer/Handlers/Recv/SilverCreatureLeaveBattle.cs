using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureLeaveBattle
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqSilverCreatureLeaveBattle)]
    public Task<SCResSilverCreatureLeaveBattleResult> OnPacket(NetContext ctx, CSReqSilverCreatureLeaveBattle req)
    {
        var (result, uniqId) = ctx.Player.SilverCreatures.SetLeaveBattle(req.UniqId);

        return Task.FromResult(new SCResSilverCreatureLeaveBattleResult {
            Result = (uint)result,
            UniqId = uniqId != 0 ? uniqId : req.UniqId
        });
    }
}
