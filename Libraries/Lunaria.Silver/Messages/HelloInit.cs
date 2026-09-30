namespace Lunaria.Silver;

public static class HelloInit
{
    public static byte[] Encode(ulong sessionId)
    {
        var p = new byte[8];
        BitConverter.TryWriteBytes(p, sessionId);
        return p;
    }
}
