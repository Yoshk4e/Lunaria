using Lunaria.Game.World;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleTaskActionUpdate(ILogger<HandleTaskActionUpdate> logger)
{
    [GameHandler(EClientServerCmds.CsTaskActionUpdate)]
    public async Task OnPacket(NetContext ctx, CSTaskActionUpdate req)
    {
        SCTaskActionUpdate Reject(int code)
        {
            return new SCTaskActionUpdate {
                Result = code,
                ActionId = req.ActionId,
                TaskType = req.TaskType,
                MaxProgress = 1
            };
        }

        if (!ctx.Player.HasActiveRole)
        {
            await ctx.SendAsync(Reject((int)EnmTextCode.EnmTextNotAccLogin)).ConfigureAwait(false);
            return;
        }

        var (code, outcome) = ctx.Player.ReportTaskAction(req.TaskType, req.ActionId, req.Progress);

        if (code != 0 || outcome is null)
        {
            logger.LogDebug("task action {TaskType}/{ActionId} rejected: {Code}", req.TaskType, req.ActionId, code);
            await ctx.SendAsync(Reject(code)).ConfigureAwait(false);
            return;
        }

        await ctx.NotifyAsync(outcome.EffectNotifications).ConfigureAwait(false);

        await ctx.SendAsync(new SCTaskActionUpdate {
            Result = 0,
            ActionId = req.ActionId,
            TaskType = req.TaskType,
            Progress = outcome.Progress.Progress,
            MaxProgress = outcome.Progress.MaxProgress
        }).ConfigureAwait(false);

        await ctx.NotifyAsync(outcome.Notifications).ConfigureAwait(false);

        if (outcome.Progress.TaskCompleted)
            logger.LogDebug(
                "task {TaskType}/{TaskId} finished by action {ActionId}",
                outcome.Progress.TaskType, outcome.Progress.TaskId, req.ActionId);

        if (ctx.Player.Map.Phase == MapPhase.Loaded)
            foreach (var arrival in ctx.Player.SettleMapArrival(ctx.Player.Map.MapId))
            {
                await ctx.NotifyAsync(arrival.AllNotifications).ConfigureAwait(false);
            }
    }
}
