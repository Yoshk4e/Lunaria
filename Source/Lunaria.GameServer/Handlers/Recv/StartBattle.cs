using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleStartBattle
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsStartBattle)]
    public Task<SCStartBattle> OnPacket(NetContext ctx, CSStartBattle req)
    {
        return Task.FromResult(new SCStartBattle {
            Ret = ctx.Player.StartBattle(req.BattleType, req.BattleFieldId),
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId
        });
    }
}
