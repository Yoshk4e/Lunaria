using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedStaminaExchange
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedStaminaExchange)]
    public async Task<SCWantedStaminaExchange> OnPacket(NetContext ctx, CSWantedStaminaExchange req)
    {
        var (result, delivery, _) = ctx.Player.WantedStaminaExchange();

        if (result != 0)
            return new SCWantedStaminaExchange { Result = result };

        if (delivery.HasChanges)
        {
            await ctx.NotifyAsync(delivery.Presentation.Where(message => message is not SCAwardShowNtf))
                .ConfigureAwait(false);
        }

        var reply = new SCWantedStaminaExchange { Result = 0 };

        reply.Items.AddRange(ctx.Player.RewardItems(delivery.Credited.Concat(delivery.Stored)));
        return reply;
    }
}
