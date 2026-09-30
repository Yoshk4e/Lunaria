using System.Text.Json.Serialization;

namespace Lunaria.GameServer.Gateway;

public sealed record HeartbeatRequest
{
    [JsonPropertyName("instance_id")]
    public required string InstanceId { get; init; }

    public required string State { get; init; }

    [JsonPropertyName("player_count")]
    public required int PlayerCount { get; init; }
}
