using System.Text.Json.Serialization;

namespace Lunaria.QueryGateway;

public sealed record InstanceSummary(
    string Id,
    string Region,
    string Ip,
    int Port,
    List<ServerAddr> Addrs,
    string State,
    [property: JsonPropertyName("player_count")]
    int PlayerCount,
    [property: JsonPropertyName("max_players")]
    int MaxPlayers
);
