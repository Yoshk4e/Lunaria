using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record ServerInfoResponse
{
    [JsonPropertyName("Code")]
    public int Code { get; init; }

    [JsonPropertyName("ErrorMsg")]
    public string ErrorMsg { get; init; } = "";

    [JsonPropertyName("server_info")]
    public required List<ServerInfoEntry> ServerInfo { get; init; }

    [JsonPropertyName("auth_info")]
    public required AuthInfo AuthInfo { get; init; }
}
