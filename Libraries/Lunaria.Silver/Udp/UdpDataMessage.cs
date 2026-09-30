namespace Lunaria.Silver;

public static class UdpDataMessage
{
    public static byte[] Encode(AesSession aes, ulong sessionId, ReadOnlySpan<byte> plaintext)
    {
        var encrypted = aes.Encrypt(plaintext);
        var datagram = new byte[UdpDatagram.HeaderLength + encrypted.Ciphertext.Length];
        datagram[0] = (byte)UdpType.Data;
        BitConverter.GetBytes(sessionId).CopyTo(datagram, index: 2);
        BitConverter.GetBytes(encrypted.PlaintextLength).CopyTo(datagram, index: 10);
        encrypted.Ciphertext.AsSpan().CopyTo(datagram.AsSpan(UdpDatagram.HeaderLength));
        return datagram;
    }
}
