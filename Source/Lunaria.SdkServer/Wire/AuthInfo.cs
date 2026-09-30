namespace Lunaria.SdkServer.Wire;

public sealed record AuthInfo
{
    public string Phone { get; init; } = "";
    public string Mail { get; init; } = "";
}
