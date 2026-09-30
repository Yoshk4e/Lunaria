using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record LoginData
{
    [JsonPropertyName("channelToken")]
    public required string ChannelToken { get; init; }

    [JsonPropertyName("channelUid")]
    public required string ChannelUid { get; init; }

    [JsonPropertyName("expireTime")]
    public required long ExpireTime { get; init; }

    [JsonPropertyName("heiToken")]
    public required string HeiToken { get; init; }

    [JsonPropertyName("sdkUid")]
    public required string SdkUid { get; init; }

    [JsonPropertyName("subChannelName")]
    public required string SubChannelName { get; init; }

    [JsonPropertyName("unionid")]
    public required string Unionid { get; init; }
}
