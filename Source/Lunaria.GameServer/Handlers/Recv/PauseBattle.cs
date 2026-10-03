using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandlePauseBattle
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsPauseBattle)]
    public Task<SCPauseBattle> OnPacket(NetContext ctx, CSPauseBattle req)
    {
        return Task.FromResult(new SCPauseBattle {
            Result = ctx.Player.PauseBattle(req.BattleType, req.BattleFieldId, req.Pause),
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId,
            Pause = req.Pause
        });
    }
}
