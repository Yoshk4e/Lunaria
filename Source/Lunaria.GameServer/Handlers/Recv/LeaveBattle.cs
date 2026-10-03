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

        // Sent right away: the client shows battle loot (reason 7) in its side panel without waiting for a later sync.
        if (outcome.BattleReward.HasChanges)
            await ctx.NotifyAsync(outcome.BattleReward.Presentation).ConfigureAwait(false);

        await ctx.NotifyAsync(outcome.Notifications).ConfigureAwait(false);

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
