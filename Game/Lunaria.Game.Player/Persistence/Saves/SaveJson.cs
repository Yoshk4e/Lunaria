using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunaria.Game.Player.Persistence.Saves;

internal static class SaveJson
{
    public static readonly JsonSerializerOptions Options = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
