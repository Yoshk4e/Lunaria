using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsEnter
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDungeonsEnter)]
    public async Task<SCDungeonsEnter> OnPacket(NetContext ctx, CSDungeonsEnter req)
    {
        var outcome = ctx.Player.EnterDungeon(req.DungeonsId);

        if (outcome.Code != 0)
            return new SCDungeonsEnter { Result = outcome.Code, DungeonsId = req.DungeonsId };

        return new SCDungeonsEnter { Result = 0, DungeonsId = req.DungeonsId };
    }
}
