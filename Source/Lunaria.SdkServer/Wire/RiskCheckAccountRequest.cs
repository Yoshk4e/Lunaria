using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record RiskCheckAccountRequest
{
    [JsonPropertyName("appId")]
    public string AppId { get; init; } = "";

    [JsonPropertyName("serverNode")]
    public string ServerNode { get; init; } = "";

    [JsonPropertyName("account")]
    public string Account { get; init; } = "";

    [JsonPropertyName("udid")]
    public string Udid { get; init; } = "";

    [JsonPropertyName("channelName")]
    public string ChannelName { get; init; } = "";

    [JsonPropertyName("subChannelName")]
    public string SubChannelName { get; init; } = "";

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; init; } = "";

    [JsonPropertyName("channelUid")]
    public string ChannelUid { get; init; } = "";

    [JsonPropertyName("versionCode")]
    public string VersionCode { get; init; } = "";

    [JsonPropertyName("gameHotfixVersion")]
    public string GameHotfixVersion { get; init; } = "";

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "";

    [JsonPropertyName("extra")]
    public string Extra { get; init; } = "";
}
