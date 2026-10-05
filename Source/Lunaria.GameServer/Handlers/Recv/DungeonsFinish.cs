using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsFinish
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDungeonsFinish)]
    public async Task<SCDungeonsFinish> OnPacket(NetContext ctx, CSDungeonsFinish req)
    {
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

            reply.RewardList.AddRange(ctx.Player.RewardItems(rewards));

            await ctx.NotifyAsync(delivery.Presentation)
                .ConfigureAwait(false);
        }

        await ctx.NotifyAsync(ctx.Player.Dungeons.ToDataNotification(ctx.Player.UtcNow))
            .ConfigureAwait(false);

        // The horde panel keeps its best score and claimed stars from SC_HORDE_DATA_NTF
        // (s_CSM_RDS_ZombieWave.OnZombieWaveDataNtf); the full data only arrives at login.
        if (horde is not null)
            await ctx.NotifyAsync(new SCHordeDataNtf {
                    Id = horde.HordeId, KillCount = horde.KillCount, StarAward = horde.StarAward
                })
                .ConfigureAwait(false);

        return reply;
    }
}
