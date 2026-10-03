using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterUpdateTmpTeam
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsCharacterUpdateTmpTeam)]
    public Task<SCCharacterUpdateTmpTeam> OnPacket(NetContext ctx, CSCharacterUpdateTmpTeam req)
    {
        var (result, team) = ctx.Player.UpdateTemporaryTeam(req.TeamType, req.TeamSrc, req.TeamData);

        return Task.FromResult(new SCCharacterUpdateTmpTeam {
            Result = result,
            TeamType = req.TeamType,
            TeamSrc = req.TeamSrc,
            TeamData = team
        });
    }
}
