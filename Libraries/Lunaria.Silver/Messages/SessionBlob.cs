using System.Security.Cryptography;

namespace Lunaria.Silver;

public readonly record struct SessionBlob(byte[] Bytes)
{
    public static SessionBlob Random()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return new SessionBlob(bytes);
    }
}
