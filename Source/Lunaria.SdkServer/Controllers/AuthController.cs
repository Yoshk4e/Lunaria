using System.Text.Json;
using Lunaria.SdkServer.Persistence;
using Lunaria.SdkServer.Services;
using Lunaria.SdkServer.Wire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Lunaria.SdkServer.Controllers;


[ApiController]
public sealed class AuthController(
    SdkCrypto crypto,
    UserRepository users,
    SessionStore sessions,
    PasswordHasher hasher,
    LastLoginTracker lastLogin,
    IOptions<SdkServerOptions> options,
    ILogger<AuthController> logger
) : ControllerBase
{
    private const int LoginFailureLockThreshold = 5;

    [HttpPost("/api/safeLogin")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> SafeLogin()
    {
        var body = await ReadBodyAsync().ConfigureAwait(false);
        var parse = await ParseEnvelope<SdkLoginRequest>(body).ConfigureAwait(false);


        if (parse.Error is {} envelopeError)
            return AuthFailure(SdkErrorCodes.InvalidExtraParams, envelopeError);

        var payload = parse.Value!;

        logger.LogInformation("SDK login request: platform {Platform}, channel {Channel}, sub {Sub}, app {AppId}, node {Node}",
            payload.Platform, payload.ChannelName, payload.SubChannelName, payload.AppId, payload.ServerNode);

        if (WireJson.Deserialize<ExtraParamsEmail>(payload.ExtraParams) is not {} extraParams)
            return AuthFailure(SdkErrorCodes.InvalidExtraParams, "Failed to parse extraParams");

        if (!IsChannelSupported(payload.ChannelName, payload.SubChannelName))
        {
            logger.LogWarning("unsupported channel {Channel}/{SubChannel}", payload.ChannelName, payload.SubChannelName);

            return AuthFailure(SdkErrorCodes.ChannelNotSupported,
                $"Unsupported channel: {payload.ChannelName}, sub_channel: {payload.SubChannelName}");
        }

        var user = await users.FindByEmailAsync(extraParams.Account).ConfigureAwait(false);

        if (user is null)
        {
            await CountFailureAsync(extraParams.Account).ConfigureAwait(false);
            logger.LogWarning("authentication failed: unknown account");

            return AuthFailure(SdkErrorCodes.InvalidCredentials,
                $"Invalid credentials for account: {extraParams.Account}");
        }

        if (user.Locked)
        {
            await CountFailureAsync(extraParams.Account).ConfigureAwait(false);
            return AuthFailure(SdkErrorCodes.AccountLocked, $"Account locked: {extraParams.Account}");
        }

        if (!hasher.Verify(extraParams.Password, user.PasswordHash))
        {
            await CountFailureAsync(extraParams.Account).ConfigureAwait(false);
            logger.LogWarning("authentication failed: bad password");

            return AuthFailure(SdkErrorCodes.InvalidCredentials,
                $"Invalid credentials for account: {extraParams.Account}");
        }

        await users.ResetFailedAttemptsAsync(extraParams.Account).ConfigureAwait(false);

        try
        {
            await users.UpdateLastLoginAsync(user.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to update last login");
        }

        long ttl = options.Value.TokenTtlSeconds;
        var (heiToken, channelToken, sdkUid, channelUid) = sessions.CreateSession(user.Id, extraParams.Account, ttl);
        lastLogin.Record(extraParams.Account);
        logger.LogInformation("login successful: sdk_uid {SdkUid}, channel_uid {ChannelUid}", sdkUid, channelUid);

        var loginData = new LoginData {
            ChannelToken = channelToken,
            ChannelUid = channelUid,
            ExpireTime = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ttl) * 1000,
            HeiToken = heiToken,
            SdkUid = sdkUid,
            SubChannelName = payload.SubChannelName,
            Unionid = extraParams.Account
        };

        var encrypted = crypto.Encrypt(WireJson.Serialize(loginData));
        return Ok(new SafeLoginResponse(Code: 0, "", encrypted));
    }

    [HttpPost("/api/risk/checkAccount")]
    [EnableRateLimiting("risk")]
    public async Task<IActionResult> CheckRiskAccount()
    {
        var body = await ReadBodyAsync().ConfigureAwait(false);
        var parse = await ParseEnvelope<RiskCheckAccountRequest>(body).ConfigureAwait(false);

        if (parse.Error is {} error)
        {
            return Ok(new RiskCheckAccountResponse(parse.Code ?? 0, error,
                EncryptedResult(new AccountCheckResult())));
        }

        var request = parse.Value!;

        logger.LogInformation("risk check account request: platform {Platform}, channel {Channel}",
            request.Platform, request.ChannelName);

        var accountStatus = 0;

        try
        {
            var user = await users.FindByEmailAsync(request.Account).ConfigureAwait(false);

            accountStatus = user switch {
                { Locked: true } => 100,
                not null => 1,
                null => 0
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "user lookup failed during risk check");
        }

        var result = new AccountCheckResult {
            AccountStatus = accountStatus,
            FirstBinding = false,
            Matching = accountStatus == 1
        };

        return Ok(new RiskCheckAccountResponse(Code: 0, "", EncryptedResult(result)));
    }

    private async Task<string> ReadBodyAsync()
    {
        using var reader = new StreamReader(Request.Body);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private async ValueTask<Envelope<T>> ParseEnvelope<T>(string body) where T : class
    {
        var isJson = Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;
        string? dataField = null;

        if (isJson)
        {
            try
            {
                using var document = JsonDocument.Parse(body);

                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.String)
                {
                    dataField = data.GetString();
                }
            }
            catch (JsonException)
            {
            }
        } else
        {
            var fields = ParseFormBody(body);
            dataField = fields.GetValueOrDefault("data");
        }

        if (dataField is not null)
        {
            if (dataField.Length == 0)
                return new Envelope<T>(Value: null, Code: 1, "Empty data field");

            try
            {
                var decrypted = crypto.Decrypt(dataField);

                if (WireJson.Deserialize<T>(decrypted) is {} parsed)
                    return new Envelope<T>(parsed, Code: null, Error: null);

                return new Envelope<T>(Value: null, Code: 2, "Invalid payload");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("data decrypt failed: {Message}", ex.Message);
                return new Envelope<T>(Value: null, Code: 3, $"Decryption error: {ex.Message}");
            }
        }

        if (WireJson.Deserialize<T>(body) is {} direct)
            return new Envelope<T>(direct, Code: null, Error: null);

        return new Envelope<T>(Value: null, Code: 4, "Invalid request");
    }

    private static Dictionary<string, string> ParseFormBody(string body)
    {
        var result = new Dictionary<string, string>();

        foreach (var pair in body.Split(separator: '&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(separator: '=', count: 2);

            if (parts.Length == 2)
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
        }
        return result;
    }

    private string EncryptedResult(AccountCheckResult result)
    {
        try
        {
            return crypto.Encrypt(WireJson.Serialize(result));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("failed to encrypt risk check result: {Message}", ex.Message);
            return "";
        }
    }

    private IActionResult AuthFailure(int code, string message) =>
        Unauthorized(new SafeLoginResponse(code, message));

    private async Task CountFailureAsync(string account)
    {
        try
        {
            await users.IncrementAndLockIfOverAsync(account, LoginFailureLockThreshold).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to increment failed attempts");
        }
    }

    private static bool IsChannelSupported(string channel, string subChannel) =>
        (channel, subChannel) switch {
            ("email", "onekey") => true,
            ("email", "default") => true,
            ("guest", "device") => true,
            ("device", "auto") => true,
            _ => false
        };

    private sealed record Envelope<T>(T? Value, int? Code, string? Error);
}
