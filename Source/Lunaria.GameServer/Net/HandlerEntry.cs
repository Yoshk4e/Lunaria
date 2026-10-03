using Google.Protobuf;

namespace Lunaria.GameServer.Net;


public sealed record HandlerEntry(
    uint CmdId,
    Type RequestType,
    Func<ByteString, object> Parse,
    Func<NetContext, object, ValueTask> Invoke,
    bool ExpectsReply,
    RequireLoginAttribute? Login,
    Func<NetContext, object, ValueTask>? RejectLogin
);
