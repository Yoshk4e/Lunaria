using Google.Protobuf;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleNoticeList
{
    [GameHandler(EClientServerCmds.CsReqNoticeList)]
    public Task<SCNoticeList> OnPacket(NetContext ctx, CSNoticeList req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCNoticeList { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var reply = new SCNoticeList { Result = 0 };

        foreach (var notice in ctx.Assets.Notices.Published(DateTimeOffset.UtcNow))
        {
            reply.NoticeList.Add(new NoticeData {
                NoticeId = notice.NoticeId,
                NoticeType = notice.NoticeType,
                ShortTitle = ByteString.CopyFromUtf8(notice.ShortTitle),
                LongTitle = ByteString.CopyFromUtf8(notice.LongTitle),
                Content = ByteString.CopyFromUtf8(notice.Content),
                PushTime = (ulong)notice.PushTime,
                EndTime = (ulong)notice.EndTime,
                LoginForcePopup = notice.LoginForcePopup ? 1u : 0u
            });
        }

        return Task.FromResult(reply);
    }
}
