using Lunaria.Game.World;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleTaskList
{
    [GameHandler(EClientServerCmds.CsReqTaskList)]
    public async Task OnPacket(NetContext ctx, CSTaskList req)
    {
        if (!ctx.Player.HasActiveRole)
        {
            await ctx.SendAsync(new SCTaskList {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            }).ConfigureAwait(false);
            return;
        }

        if (ctx.Player.TasksBootstrapped)
        {
            await ctx.SendAsync(new SCTaskList {
                Result = 0,
                TaskData = ctx.Player.Tasks.ToPlayerTaskData()
            }).ConfigureAwait(false);
            await SettleArrivalsAsync(ctx).ConfigureAwait(false);
            return;
        }

        await ctx.SendAsync(new SCTaskList {
            Result = 0,
            TaskData = ctx.Player.Tasks.ToBootstrapPlayerTaskData()
        }).ConfigureAwait(false);

        await ctx.NotifyAsync(new SCTaskList {
            Result = 0,
            TaskData = ctx.Player.Tasks.ToPlayerTaskData()
        }).ConfigureAwait(false);
        ctx.Player.TasksBootstrapped = true;
        await SettleArrivalsAsync(ctx).ConfigureAwait(false);
    }

    private async Task SettleArrivalsAsync(NetContext ctx)
    {
        if (ctx.Player.Map.Phase != MapPhase.Loaded)
            return;

        foreach (var outcome in ctx.Player.SettleMapArrival(ctx.Player.Map.MapId))
        {
            await ctx.NotifyAsync(outcome.AllNotifications).ConfigureAwait(false);
        }
    }
}
