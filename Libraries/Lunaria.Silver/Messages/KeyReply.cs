namespace Lunaria.Silver;


public static class KeyReply
{
    public static byte[] Encode(PublicKey echoedClientKey, PublicKey serverKey)
    {
        var p = new byte[Kex.PayloadLength];
        echoedClientKey.WriteLittleEndian(p.AsSpan(Kex.Blob1Offset, length: 16));
        p[Kex.MarkerOffset] = unchecked((byte)Wire.ProtocolMarker);
        p[Kex.MarkerOffset + 1] = Wire.ProtocolMarker >> 8;
        serverKey.WriteLittleEndian(p.AsSpan(Kex.PublicKeyOffset, length: 16));
        return p;
    }
}
