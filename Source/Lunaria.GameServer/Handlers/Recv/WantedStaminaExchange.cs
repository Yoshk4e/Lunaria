using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedStaminaExchange
{
    [GameHandler(EClientServerCmds.CsWantedStaminaExchange)]
    public async Task<SCWantedStaminaExchange> OnPacket(NetContext ctx, CSWantedStaminaExchange req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCWantedStaminaExchange { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var (result, delivery, _) = ctx.Player.WantedStaminaExchange();

        if (result != 0)
            return new SCWantedStaminaExchange { Result = result };

        if (delivery.HasChanges)
        {
            await ctx.NotifyAsync(delivery.Presentation.Where(message => message is not SCAwardShowNtf))
                .ConfigureAwait(false);
        }

        var reply = new SCWantedStaminaExchange { Result = 0 };

        reply.Items.AddRange(delivery.Credited.Concat(delivery.Stored).Select(grant => new CmdItem {
            ItemId = grant.ItemId,
            ItemNum = grant.Count
        }));
        return reply;
    }
}
