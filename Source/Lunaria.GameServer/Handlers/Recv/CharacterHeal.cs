using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterHeal
{
    [GameHandler(EClientServerCmds.CsCharacterHealReq)]
    public async Task<SCCharacterHealRes> OnPacket(NetContext ctx, CSCharacterHealReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCCharacterHealRes { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        ctx.Player.HealRoster();

        return new SCCharacterHealRes { Result = 0 };
    }
}
