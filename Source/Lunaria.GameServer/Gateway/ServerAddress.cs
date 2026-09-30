namespace Lunaria.GameServer.Gateway;
public sealed record ServerAddress
{
    public required string Ip { get; init; }
    public required int Port { get; init; }
}
