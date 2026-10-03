using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureRelease
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsSilverCreatureRelease)]
    public Task<SCSilverCreatureReleaseResult> OnPacket(NetContext ctx, CSSilverCreatureRelease req)
    {
        var (result, change) = ctx.Player.ReleaseSilverCreatures(req.UniqId);

        return Task.FromResult(new SCSilverCreatureReleaseResult {
            Result = (uint)result,
            Change = change
        });
    }
}
