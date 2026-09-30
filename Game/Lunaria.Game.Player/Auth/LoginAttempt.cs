using System.Text;
using Google.Protobuf;
using Msg;

namespace Lunaria.Game.Player.Auth;

public sealed record LoginAttempt
{
    public required string Userid { get; init; }
    public required string ChannelName { get; init; }
    public required string ChannelUid { get; init; }
    public required string Udid { get; init; }
    public required string Version { get; init; }
    public required int HeiTokenLength { get; init; }
    public required int ChannelTokenLength { get; init; }

    public static LoginAttempt FromRequest(CSAccountLogin request) => new() {
        Userid = Encoding.UTF8.GetString(request.Userid.Span).Trim(),
        ChannelName = ToString(request.ChannelName),
        ChannelUid = ToString(request.ChannelUid),
        Udid = ToString(request.Udid),
        Version = ToString(request.Version),
        HeiTokenLength = request.HeiToken.Length,
        ChannelTokenLength = request.ChannelToken.Length
    };

    private static string ToString(ByteString bytes)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes.Span);
        }
        catch (DecoderFallbackException)
        {
            return string.Empty;
        }
    }
}
