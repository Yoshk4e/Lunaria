using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterHeal
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsCharacterHealReq)]
    public async Task<SCCharacterHealRes> OnPacket(NetContext ctx, CSCharacterHealReq req)
    {
        ctx.Player.HealRoster();

        return new SCCharacterHealRes { Result = 0 };
    }
}
