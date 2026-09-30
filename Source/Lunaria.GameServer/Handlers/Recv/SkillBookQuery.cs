using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSkillBookQuery
{
    [GameHandler(EClientServerCmds.CsSkillBookQuery)]
    public Task<SCSkillBookQuery> OnPacket(NetContext ctx, CSSkillBookQuery req)
        => Task.FromResult(new SCSkillBookQuery {
            Result = 0,
            Infos = { ctx.Player.Skills.GrowthInfos() }
        });
}
