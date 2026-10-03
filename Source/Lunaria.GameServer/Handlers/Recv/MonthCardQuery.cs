using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMonthCardQuery
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMonthCardQuery)]
    public async Task<SCMonthCardQuery> OnPacket(NetContext ctx, CSMonthCardQuery req)
    {
        await ctx.NotifyAsync(ctx.Player.SettleMonthCards(ctx.Player.UtcNow)).ConfigureAwait(false);

        return new SCMonthCardQuery {
            Result = 0,
            Data = ctx.Player.MonthCards.ToMonthCardData()
        };
    }
}
