namespace Lunaria.Silver;

public static class ServerKeyExchange
{
    public static byte[] Encode(SessionBlob blob1, SessionBlob blob2)
    {
        var p = new byte[Kex.PayloadLength];
        blob1.Bytes.AsSpan().CopyTo(p.AsSpan(Kex.Blob1Offset, length: 16));
        blob2.Bytes.AsSpan().CopyTo(p.AsSpan(Kex.Blob2Offset, length: 16));
        return p;
    }
}
