using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleEnterBattle
{
    [GameHandler(EClientServerCmds.CsEnterBattle)]
    public Task<SCEnterBattle> OnPacket(NetContext ctx, CSEnterBattle req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCEnterBattle {
                Ret = (int)EnmTextCode.EnmTextNotAccLogin,
                BattleType = req.BattleType,
                BattleFieldId = req.BattleFieldId
            });

        return Task.FromResult(new SCEnterBattle {
            Ret = ctx.Player.EnterBattle(req.BattleType, req.BattleFieldId, req.BattleInstId, req.MonsterFromType),
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId
        });
    }
}
