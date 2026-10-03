using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDoGacha(ILogger<HandleDoGacha> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDoGachaReq)]
    public async Task<SCDoGachaResult> OnPacket(NetContext ctx, CSDoGacha req)
    {
        SCDoGachaResult Reject(int code)
        {
            return new SCDoGachaResult { Result = code };
        }

        var now = ctx.Player.UtcNow;
        var (code, delivery) = ctx.Player.DoGacha(req.PoolId, req.IsMult, now, ctx.Player.RandomSources.Gacha);

        if (code != 0 || delivery is null)
            return Reject(code);

        await ctx.NotifyAsync(delivery.Delivery.Presentation)
            .ConfigureAwait(false);


        logger.LogDebug(
            "gacha pool {PoolId} {Count}x: {Newcomers} newcomers, {Motives} motives",
            req.PoolId, req.IsMult ? 10 : 1, delivery.Newcomers.Count, delivery.NewMotives.Count);

        return new SCDoGachaResult {
            Result = 0,
            Cost = delivery.Cost,
            PoolInfo = delivery.PoolInfo
        };
    }
}
