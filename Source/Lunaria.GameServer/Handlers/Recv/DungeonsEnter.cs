using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsEnter
{
    [GameHandler(EClientServerCmds.CsDungeonsEnter)]
    public async Task<SCDungeonsEnter> OnPacket(NetContext ctx, CSDungeonsEnter req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCDungeonsEnter {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                DungeonsId = req.DungeonsId
            };

        var outcome = ctx.Player.EnterDungeon(req.DungeonsId);

        if (outcome.Code != 0)
            return new SCDungeonsEnter { Result = outcome.Code, DungeonsId = req.DungeonsId };

        return new SCDungeonsEnter { Result = 0, DungeonsId = req.DungeonsId };
    }
}
