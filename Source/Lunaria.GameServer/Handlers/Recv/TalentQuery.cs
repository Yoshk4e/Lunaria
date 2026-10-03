using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleTalentQuery
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsTalentQuery)]
    public Task<SCTalentQuery> OnPacket(NetContext ctx, CSTalentQuery req)
        => Task.FromResult(new SCTalentQuery {
            Result = 0,
            Infos = { ctx.Player.Skills.TalentInfos() }
        });
}
