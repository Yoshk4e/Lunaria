namespace Lunaria.SdkServer.Wire;

/// <summary>
/// Codes the HappyElements SDK turns into its own messages. The passport web bundle maps 45 to
/// he_login_error_account_pwderror ("Wrong account or password"), and the client showed 1001 as
/// he_login_error_frequently ("Too many login attempts").
/// </summary>
public static class SdkErrorCodes
{
    public const int InvalidCredentials = 45;
    public const int AccountLocked = 1001;
    public const int ChannelNotSupported = 2001;
    public const int InvalidExtraParams = 2002;
    public const int ServerError = 5000;
}
