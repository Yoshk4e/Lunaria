using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureCombine
{
    [GameHandler(EClientServerCmds.CsSilverCreatureCombine)]
    public Task<SCSilverCreatureCombineResult> OnPacket(NetContext ctx, CSSilverCreatureCombine req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCSilverCreatureCombineResult {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            });

        var (result, change) = ctx.Player.CombineSilverCreatures(req.UniqId);

        return Task.FromResult(new SCSilverCreatureCombineResult {
            Result = (uint)result,
            Change = change
        });
    }
}
