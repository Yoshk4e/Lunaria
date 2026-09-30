namespace Lunaria.Silver;

public static class HeartbeatMessage
{
    public static byte[] Encode(ulong timestampMs, uint metric)
    {
        var p = new byte[12];
        BitConverter.TryWriteBytes(p, timestampMs);
        BitConverter.TryWriteBytes(p.AsSpan(start: 8, length: 4), metric);
        return p;
    }

    public static byte[] Now(long unixEpochMilliseconds, long uptimeMicroseconds)
        => Encode(unchecked((ulong)unixEpochMilliseconds), unchecked((uint)uptimeMicroseconds));
}
