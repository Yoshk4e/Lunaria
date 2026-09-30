namespace Lunaria.SdkServer.Wire;

public sealed record SafeLoginResponse(int Code, string Message, string? Ret = null);
