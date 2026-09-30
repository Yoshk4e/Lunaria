using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleStartBattle
{
    [GameHandler(EClientServerCmds.CsStartBattle)]
    public Task<SCStartBattle> OnPacket(NetContext ctx, CSStartBattle req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCStartBattle {
                Ret = (int)EnmTextCode.EnmTextNotAccLogin,
                BattleType = req.BattleType,
                BattleFieldId = req.BattleFieldId
            });

        return Task.FromResult(new SCStartBattle {
            Ret = ctx.Player.Battles.Start(req.BattleType, req.BattleFieldId),
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId
        });
    }
}
