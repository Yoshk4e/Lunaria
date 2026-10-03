using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleWantedOver
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedOver)]
    public async Task<SCWantedOver> OnPacket(NetContext ctx, CSWantedOver req)
    {
        var (code, settlement, delivery) = ctx.Player.WantedOver();

        if (code != 0 || settlement is null)
            return new SCWantedOver { Result = code };

        if (delivery is not null)
        {
            await ctx.NotifyAsync(delivery.Presentation)
                .ConfigureAwait(false);
        }

        return settlement;
    }
}
