using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveQuery(ILogger<HandleMotiveQuery> logger)
{
    [GameHandler(EClientServerCmds.CsMotiveQuery)]
    public Task<SCMotiveQuery> OnPacket(NetContext ctx, CSMotiveQuery req)
    {
        var elems = ctx.Player.Motives.ListData();
        logger.LogDebug("motive query: {Count} entries", elems.Count);

        return Task.FromResult(new SCMotiveQuery {
            Result = 0,
            MotiveElems = { elems }
        });
    }
}
