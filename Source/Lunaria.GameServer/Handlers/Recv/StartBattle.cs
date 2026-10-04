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

    /// <summary>
    /// The CBT1 Wanted battle module (s_WP_WPG_WantedPosterBattleSystem) sends its start with the SC_START_BATTLE
    /// id and the SCStartBattle layout. It waits for no reply; without this the battle never starts and its
    /// victory is refused.
    /// </summary>
    [RequireLogin]
    [GameHandler(EClientServerCmds.ScStartBattle)]
    public Task OnMisroutedStart(NetContext ctx, SCStartBattle req)
    {
        ctx.Player.StartBattle(req.BattleType, req.BattleFieldId);
        return Task.CompletedTask;
    }
}
