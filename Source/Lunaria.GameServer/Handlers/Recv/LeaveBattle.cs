using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleLeaveBattle
{
    [GameHandler(EClientServerCmds.CsLeaveBattle)]
    public async Task OnPacket(NetContext ctx, CSLeaveBattle req)
    {
        var outcome = ctx.Player.LeaveBattle(req);

        await ctx.SendAsync(new SCLeaveBattle {
            Ret = outcome.Result,
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId,
            BattleResult = req.BattleResult,
            ExtraInfo = req.BattleResult == EBattleResultType.EnmBattleResultTypeDeadFail ?
                new BattleExtraInfo {
                    RebornMapId = ctx.Player.Map.MapId,
                    RebornSavepoint = ctx.Player.Map.Savepoint
                } :
                null
        }).ConfigureAwait(false);

        if (outcome.WantedStepCompleted)
        {
            if (outcome.WantedReward.HasChanges)
            {
                await ctx.NotifyAsync(outcome.WantedReward.Presentation).ConfigureAwait(false);
            }

            if (ctx.Player.Wanted.ToStepNotification() is {} step)
                await ctx.NotifyAsync(step).ConfigureAwait(false);
        }
    }
}
