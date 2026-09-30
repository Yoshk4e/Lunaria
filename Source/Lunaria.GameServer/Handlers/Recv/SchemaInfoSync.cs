using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSchemaInfoSync(ILogger<HandleSchemaInfoSync> logger)
{
    [GameHandler(EClientServerCmds.CsSchemaInfoSync)]
    public Task<SCSchemaInfoSync> OnPacket(NetContext ctx, CSSchemaInfoSync req)
    {
        logger.LogInformation(
            "schema info synced: game_version {Version}, version_code {Code}, platform {Platform}, udid {Udid}",
            req.GameVersion.ToStringUtf8(), req.GameVersionCode.ToStringUtf8(),
            req.UserPlatform.ToStringUtf8(), req.Udid.ToStringUtf8());

        return Task.FromResult(new SCSchemaInfoSync {
            Result = 0,
            LoginId = ctx.Player.SessionId
        });
    }
}
