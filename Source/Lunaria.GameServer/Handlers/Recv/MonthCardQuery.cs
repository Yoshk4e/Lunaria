using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMonthCardQuery
{
    [GameHandler(EClientServerCmds.CsMonthCardQuery)]
    public async Task<SCMonthCardQuery> OnPacket(NetContext ctx, CSMonthCardQuery req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCMonthCardQuery { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        await ctx.NotifyAsync(ctx.Player.SettleMonthCards(DateTimeOffset.UtcNow)).ConfigureAwait(false);

        return new SCMonthCardQuery {
            Result = 0,
            Data = ctx.Player.MonthCards.ToMonthCardData()
        };
    }
}
