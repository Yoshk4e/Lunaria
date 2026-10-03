using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSigninActivityData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsSigninActivityData)]
    public async Task<SCSignInActivityData> OnPacket(NetContext ctx, CSSignInActivityData req)
    {
        var (result, data) = ctx.Player.QuerySignIn(req.ActivityId);


        var reply = new SCSignInActivityData {
            Result = result,
            ActivityData = data ?? new SignInActivityData { ActivityId = req.ActivityId }
        };

        if (result == 0 && data is not null && !ctx.Player.SignInPopSent)
        {
            ctx.Player.SignInPopSent = true;

            if (ctx.Player.SignIn.HasClaimableDay(req.ActivityId))
            {
                await ctx.NotifyAsync(new SCSignInActivityPop { ActivityData = data })
                    .ConfigureAwait(false);
            }
        }

        return reply;
    }
}
