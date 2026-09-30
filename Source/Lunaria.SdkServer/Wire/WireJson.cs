using System.Text.Json;

namespace Lunaria.SdkServer.Wire;

public static class WireJson
{
    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value);

    public static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
