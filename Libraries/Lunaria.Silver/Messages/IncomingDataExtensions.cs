namespace Lunaria.Silver;

public static class IncomingDataExtensions
{
    public static byte[] Decrypt(this Incoming.Data data, AesSession session)
    {
        var plain = session.Decrypt(data.Ciphertext);
        return plain.AsSpan(start: 0, data.PlaintextLength).ToArray();
    }
}
