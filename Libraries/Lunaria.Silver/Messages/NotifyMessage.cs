namespace Lunaria.Silver;

public static class NotifyMessage
{
    public static byte[] Encode(ushort establishCode, ulong sessionId)
    {
        var p = new byte[10];
        p[0] = (byte)establishCode;
        p[1] = (byte)(establishCode >> 8);
        BitConverter.TryWriteBytes(p.AsSpan(start: 2, length: 8), sessionId);
        return p;
    }
}
