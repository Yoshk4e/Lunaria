using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedChooseAward
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedChooseAward)]
    public async Task<SCWantedChooseAward> OnPacket(NetContext ctx, CSWantedChooseAward req)
    {
        var (result, finished, step) = ctx.Player.ChooseWantedAward(
            req.StepAwardId, req.Award, req.ReplacedBionicsUniqid);

        if (result != 0)
            return new SCWantedChooseAward {
                Result = result,
                StepAwardId = req.StepAwardId,
                Award = req.Award
            };


        if (step is not null)
            await ctx.NotifyAsync(step).ConfigureAwait(false);

        return new SCWantedChooseAward {
            Result = 0,
            StepAwardId = req.StepAwardId,
            Award = req.Award,
            Finish = finished
        };
    }
}
