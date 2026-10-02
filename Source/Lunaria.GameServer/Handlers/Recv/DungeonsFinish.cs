using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsFinish
{
    [GameHandler(EClientServerCmds.CsDungeonsFinish)]
    public async Task<SCDungeonsFinish> OnPacket(NetContext ctx, CSDungeonsFinish req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCDungeonsFinish { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var (code, delivery, horde) = ctx.Player.FinishDungeon(
            req.DungeonsId, req.Victory, req.Leave, req.HordeData?.KillCount ?? 0);

        if (code != 0)
            return new SCDungeonsFinish { Result = code };

        var reply = new SCDungeonsFinish {
            Result = 0,
            HordeData = new HordeFinishDataRes { KillCount = horde?.KillCount ?? req.HordeData?.KillCount ?? 0 }
        };

        if (delivery is not null)
        {
            var rewards = delivery.Credited.Concat(delivery.Stored).ToList();

            reply.RewardList.AddRange(rewards.Select(grant => new CmdItem {
                ItemId = grant.ItemId,
                ItemNum = grant.Count
            }));

            await ctx.NotifyAsync(delivery.Presentation)
                .ConfigureAwait(false);
        }

        await ctx.NotifyAsync(ctx.Player.Dungeons.ToDataNotification(ctx.Player.UtcNow))
            .ConfigureAwait(false);

        return reply;
    }
}
