using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record SdkLoginRequest
{
    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "";

    [JsonPropertyName("channelName")]
    public string ChannelName { get; init; } = "";

    [JsonPropertyName("subChannelName")]
    public string SubChannelName { get; init; } = "";

    [JsonPropertyName("appId")]
    public string AppId { get; init; } = "";

    [JsonPropertyName("udid")]
    public string Udid { get; init; } = "";

    [JsonPropertyName("serverNode")]
    public string ServerNode { get; init; } = "";

    [JsonPropertyName("extraParams")]
    public string ExtraParams { get; init; } = "";

    [JsonPropertyName("channelUid")]
    public string ChannelUid { get; init; } = "";

    [JsonPropertyName("channelToken")]
    public string ChannelToken { get; init; } = "";

    [JsonPropertyName("deviceModel")]
    public string DeviceModel { get; init; } = "";

    [JsonPropertyName("deviceBrand")]
    public string DeviceBrand { get; init; } = "";
}
