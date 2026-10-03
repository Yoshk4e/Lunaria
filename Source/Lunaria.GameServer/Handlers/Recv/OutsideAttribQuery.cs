using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleOutsideAttribQuery(ILogger<HandleOutsideAttribQuery> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsOutsideAttribQuery)]
    public Task<SCOutsideAttribQuery> OnPacket(NetContext ctx, CSOutsideAttribQuery req)
    {
        var wanted = req.InstId.Count == 0 ? ctx.Player.CurrentTeamMembers() : req.InstId;

        var data = wanted.Select(instId => ctx.Player.OutsideAttributes(instId)).ToList();

        foreach (var entry in data.Where(entry => entry.AttribData.Count == 0))
        {
            logger.LogWarning("no attributes for instance {InstId}", entry.InstId);
        }

        logger.LogDebug("outside attributes: {Count} instances", data.Count);
        return Task.FromResult(new SCOutsideAttribQuery { Result = 0, Data = { data } });
    }
}
