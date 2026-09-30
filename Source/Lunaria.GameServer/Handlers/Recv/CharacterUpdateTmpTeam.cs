using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterUpdateTmpTeam
{
    [GameHandler(EClientServerCmds.CsCharacterUpdateTmpTeam)]
    public Task<SCCharacterUpdateTmpTeam> OnPacket(NetContext ctx, CSCharacterUpdateTmpTeam req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCCharacterUpdateTmpTeam {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                TeamType = req.TeamType,
                TeamSrc = req.TeamSrc
            });

        var (result, team) = ctx.Player.UpdateTemporaryTeam(req.TeamType, req.TeamSrc, req.TeamData);

        return Task.FromResult(new SCCharacterUpdateTmpTeam {
            Result = result,
            TeamType = req.TeamType,
            TeamSrc = req.TeamSrc,
            TeamData = team
        });
    }
}
