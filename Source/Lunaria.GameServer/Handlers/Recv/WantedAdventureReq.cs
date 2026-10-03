using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedAdventureReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedAdventureReq)]
    public Task<SCWantedAdventureRes> OnPacket(NetContext ctx, CSWantedAdventureReq req)
    {
        var reply = new SCWantedAdventureRes { Result = 0 };

        foreach (var adventure in ctx.Player.Wanted.Adventures)
        {
            reply.Adventures.Add(new CmdWantedOneAdventure {
                AdventureId = adventure.AdventureId,
                ContentId = adventure.ContentId,
                DialogId = adventure.DialogId,
                OptionResult = adventure.OptionResult
            });
        }

        return Task.FromResult(reply);
    }
}
