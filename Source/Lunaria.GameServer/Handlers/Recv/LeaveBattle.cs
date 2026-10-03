using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleLeaveBattle
{
    [RequireLogin(Reply = typeof(SCLeaveBattle))]
    [GameHandler(EClientServerCmds.CsLeaveBattle)]
    public async Task OnPacket(NetContext ctx, CSLeaveBattle req)
    {
        var outcome = ctx.Player.LeaveBattle(req);
        var reborn = ctx.Player.Map.RebornPoint();

        await ctx.SendAsync(new SCLeaveBattle {
            Ret = outcome.Result,
            BattleType = req.BattleType,
            BattleFieldId = req.BattleFieldId,
            BattleResult = req.BattleResult,
            // A wiped hotel (Wanted) battle is retried in place, so no respawn point is sent.
            ExtraInfo = req.BattleResult == EBattleResultType.EnmBattleResultTypeDeadFail
                && req.BattleType != EBattleType.EnmBattleTypeWanted ?
                new BattleExtraInfo {
                    RebornMapId = reborn.MapId,
                    RebornSavepoint = reborn.Savepoint
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
