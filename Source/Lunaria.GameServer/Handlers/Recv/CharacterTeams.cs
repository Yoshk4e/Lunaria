using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterTeams(ILogger<HandleCharacterTeams> logger)
{
    [GameHandler(EClientServerCmds.CsCharacterTeams)]
    public Task<SCCharacterTeams> OnPacket(NetContext ctx, CSCharacterTeams req)
    {
        var currentTeam = ctx.Player.CurrentTeamData();

        if (currentTeam is null)
            logger.LogError("no current team, client will hang");

        return Task.FromResult(new SCCharacterTeams {
            Result = 0,
            Teams = { ctx.Player.Teams.TeamsData() },
            CurTeam = currentTeam
        });
    }
}
