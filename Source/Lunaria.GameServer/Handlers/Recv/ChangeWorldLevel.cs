using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleChangeWorldLevel
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsChangeWorldLevel)]
    public async Task<SCChangeWorldLevel> OnPacket(NetContext ctx, CSChangeWorldLevel req)
    {
        var (result, worldLevel) = ctx.Player.SelectWorldLevel(req.WorldLevel);

        return new SCChangeWorldLevel {
            Result = (uint)result,
            WorldLevel = worldLevel
        };
    }
}
