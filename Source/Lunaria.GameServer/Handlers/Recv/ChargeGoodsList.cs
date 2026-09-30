using Google.Protobuf;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleChargeGoodsList
{
    [GameHandler(EClientServerCmds.CsChaegrGoodsList)]
    public Task<SCChargeGoodsList> OnPacket(NetContext ctx, CSChargeGoodsList req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCChargeGoodsList { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var reply = new SCChargeGoodsList { Result = 0 };

        foreach (var good in ctx.Assets.Charge.Offered)
        {
            reply.GoodsList.Add(new ChargeGoodsCfg {
                Id = good.Id,
                ChargeType = good.ChargeType,
                SubId = good.SubId,
                Currency = ByteString.CopyFromUtf8("GEM"),
                IsShow = true,
                StartTime = (uint)good.StartTime,
                EndTime = (uint)good.EndTime,
                IsFirstCharge = ctx.Player.IsFirstChargeOf(good.Id)
            });
        }

        return Task.FromResult(reply);
    }
}
