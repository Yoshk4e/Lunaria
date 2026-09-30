using System.Text;
using Msg;

namespace Lunaria.Game.Player.Text;

public static class TextValidator
{
    /// <summary>Match the client's name length rule: ASCII counts as 1 and multibyte characters as 2.</summary>
    public const int MaxNameWeight = 14;

    public static int NameWeight(string s) =>
        s.Sum(c => Encoding.UTF8.GetByteCount(char.ToString(c)) > 1 ? 2 : 1);

    public static string? AsUtf8(ReadOnlySpan<byte> raw)
    {
        try
        {
            return Encoding.UTF8.GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    public static int? ValidatePlayerName(ReadOnlySpan<byte> raw)
    {
        var s = AsUtf8(raw);

        if (s is null || s.Length == 0)
            return (int)EnmTextCode.EnmTextIllegalRoleName;

        if (NameWeight(s) > MaxNameWeight)
            return (int)EnmTextCode.EnmTextRoleNameTooLong;

        return null;
    }

    public static int? CheckText(int eventType, ReadOnlySpan<byte> text)
    {
        var illegal = (int)EnmTextCode.EnmTextCheckTextIllegal;

        var validEvent = eventType >= (int)FenceEventType.EnmFenceEventName
                         && eventType < (int)FenceEventType.EnmFenceEventMax;

        if (!validEvent)
            return illegal;

        var s = AsUtf8(text);

        if (s is null || s.Length == 0 || s.Length > (int)EnmSizeLimit.MaxCheckTextLen)
            return illegal;

        if (eventType == (int)FenceEventType.EnmFenceEventName && NameWeight(s) > MaxNameWeight)
            return illegal;

        return null;
    }
}
