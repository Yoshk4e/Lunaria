using Lunaria.GameServer.Net;
using Lunaria.GameServer.Services;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterList(RoleSessionService sessions)
{
    [GameHandler(EClientServerCmds.CsReqCharacterList)]
    public Task<SCCharacterListRsp> OnPacket(NetContext ctx, CSCharacterListReq req) => sessions.CharacterListAsync(ctx);
}
