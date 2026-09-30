using Google.Protobuf;
using Lunaria.Game.Player.Auth;
using Lunaria.Game.Player.Persistence;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleAccountLogin(
    AccountRepository accounts,
    RoleRepository roles,
    ILogger<HandleAccountLogin> logger
)
{
    private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [GameHandler(EClientServerCmds.CsAccountLogin)]
    public async Task<SCAccountLogin> OnPacket(NetContext ctx, CSAccountLogin req)
    {
        var attempt = LoginAttempt.FromRequest(req);

        logger.LogInformation(
            "login attempt: userid {Userid}, version {Version}, channel {Channel}, channel_uid {ChannelUid}, udid {Udid}, hei_token_len {HeiTokenLen}, channel_token_len {ChannelTokenLen}",
            attempt.Userid, attempt.Version, attempt.ChannelName, attempt.ChannelUid, attempt.Udid,
            attempt.HeiTokenLength, attempt.ChannelTokenLength);

        SCAccountLogin Failure(int code)
        {
            return new SCAccountLogin { Result = code };
        }

        var identity = await ctx.Runtime.Auth.AuthenticateAsync(attempt).ConfigureAwait(false);

        if (identity is null)
            return Failure((int)EnmTextCode.EnmTextAccLoginServerNotOpenFail);

        AccountRow account;

        try
        {
            account = await accounts.UpsertOnLoginAsync(
                    identity.AccountKey, attempt.ChannelName, attempt.ChannelUid, attempt.Udid)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "account upsert failed");
            return Failure((int)EnmTextCode.EnmTextAccLoginAccountDataFail);
        }

        IReadOnlyList<RoleRow> rows;

        try
        {
            rows = await roles.ListByAccountAsync(account.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "role load failed");
            return Failure((int)EnmTextCode.EnmTextAccLoginAccountDataFail);
        }

        var bound = ctx.Player.Account.Bind(
            account.Id, account.AccountKey, account.Userid, DateTimeOffset.UtcNow);

        if (bound != 0)
        {
            logger.LogWarning("repeat account login on session {Session}", ctx.Player.SessionId);
            return Failure(bound);
        }

        ctx.Player.Account.SetOrigin(attempt.ChannelName, attempt.Udid);
        ctx.Player.Roles.Load(rows);

        logger.LogInformation(
            "account bound: id {AccountId}, session {Session}, roles {Roles}",
            account.Id, ctx.Player.SessionId, ctx.Player.Roles.Count);

        return new SCAccountLogin {
            Result = 0,
            ConnectIdentifyId = ctx.Player.SessionId,
            WorldId = 1,
            Timestamp = UnixNow(),
            BriefRole = ctx.Player.Roles.Newest() is {} newest ? new RoleListBriefInfo { RoleId = (ulong)newest.Id } : null,
            Userid = ByteString.CopyFromUtf8(ctx.Player.Account.Userid)
        };
    }
}
