using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleChangeWorldLevel
{
    [GameHandler(EClientServerCmds.CsChangeWorldLevel)]
    public async Task<SCChangeWorldLevel> OnPacket(NetContext ctx, CSChangeWorldLevel req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCChangeWorldLevel { Result = (uint)EnmTextCode.EnmTextNotAccLogin };

        var (result, worldLevel) = ctx.Player.SelectWorldLevel(req.WorldLevel);

        return new SCChangeWorldLevel {
            Result = (uint)result,
            WorldLevel = worldLevel
        };
    }
}
