using System.Text.Json.Serialization;

namespace Lunaria.QueryGateway;

public sealed record RegisterRequest(
    string Region,
    string Ip,
    int Port,
    List<ServerAddr> Addrs,
    [property: JsonPropertyName("max_players")]
    int MaxPlayers
);
