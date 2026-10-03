using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleEnterBattle
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsEnterBattle)]
    public Task<SCEnterBattle> OnPacket(NetContext ctx, CSEnterBattle req)
    {
        return Task.FromResult(new SCEnterBattle {
            Ret = ctx.Player.EnterBattle(req.BattleType, req.BattleFieldId, req.BattleInstId, req.MonsterFromType),
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId
        });
    }
}
