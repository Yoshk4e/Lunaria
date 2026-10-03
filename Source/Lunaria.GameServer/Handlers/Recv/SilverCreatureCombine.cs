using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureCombine
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsSilverCreatureCombine)]
    public Task<SCSilverCreatureCombineResult> OnPacket(NetContext ctx, CSSilverCreatureCombine req)
    {
        var (result, change) = ctx.Player.CombineSilverCreatures(req.UniqId);

        return Task.FromResult(new SCSilverCreatureCombineResult {
            Result = (uint)result,
            Change = change
        });
    }
}
