using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureRelease
{
    [GameHandler(EClientServerCmds.CsSilverCreatureRelease)]
    public Task<SCSilverCreatureReleaseResult> OnPacket(NetContext ctx, CSSilverCreatureRelease req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCSilverCreatureReleaseResult {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            });

        var (result, change) = ctx.Player.ReleaseSilverCreatures(req.UniqId);

        return Task.FromResult(new SCSilverCreatureReleaseResult {
            Result = (uint)result,
            Change = change
        });
    }
}
