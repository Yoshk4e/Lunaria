using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterTmpTeamQuery
{
    [GameHandler(EClientServerCmds.CsCharacterTmpTeamQuery)]
    public Task<SCCharacterTmpTeamQuery> OnPacket(NetContext ctx, CSCharacterTmpTeamQuery req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCCharacterTmpTeamQuery {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                TeamType = req.TeamType,
                TeamSrc = req.TeamSrc
            });

        var team = ctx.Player.QueryTemporaryTeam(req.TeamType, req.TeamSrc);

        return Task.FromResult(new SCCharacterTmpTeamQuery {
            Result = team is null ? (int)EnmTextCode.EnmTextWrongParam : 0,
            TeamType = req.TeamType,
            TeamSrc = req.TeamSrc,
            TeamData = team
        });
    }
}
