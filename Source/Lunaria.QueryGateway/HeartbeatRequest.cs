using System.Text.Json.Serialization;

namespace Lunaria.QueryGateway;

public sealed record HeartbeatRequest(
    [property: JsonPropertyName("instance_id")]
    string InstanceId,
    [property: JsonPropertyName("state")] InstanceState State,
    [property: JsonPropertyName("player_count")]
    int PlayerCount
);
