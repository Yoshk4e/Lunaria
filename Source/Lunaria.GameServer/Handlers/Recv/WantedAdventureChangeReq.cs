using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedAdventureChangeReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedAdventureChangeReq)]
    public async Task<SCWantedAdventureChangeRes> OnPacket(NetContext ctx, CSWantedAdventureChangeReq req)
    {
        var (code, completed, step, delivery) = ctx.Player.ResolveWantedAdventure(
            req.AdventureId, req.ContentId, req.DialogId);

        if (code != 0)
            return new SCWantedAdventureChangeRes { Result = code };

        if (completed && delivery.HasChanges)
        {
            await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);
        }

        if (step is not null)
            await ctx.NotifyAsync(step).ConfigureAwait(false);

        return new SCWantedAdventureChangeRes {
            Result = 0,
            Adventure = new CmdWantedOneAdventure {
                AdventureId = req.AdventureId,
                ContentId = req.ContentId,
                DialogId = req.DialogId,
                OptionResult = 0
            }
        };
    }
}
