using System.Text.Json.Serialization;

namespace Lunaria.GameServer.Gateway;

public sealed record RegisterRequest
{
    public required string Region { get; init; }
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public required List<ServerAddress> Addrs { get; init; }

    [JsonPropertyName("max_players")]
    public required int MaxPlayers { get; init; }
}
