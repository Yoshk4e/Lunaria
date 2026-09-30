namespace Lunaria.SdkServer.Wire;

public sealed record ServerAddr
{
    public required string Ip { get; init; }
    public int Port { get; init; }
}
