using System.Security.Cryptography;

namespace Lunaria.Silver;


public sealed class DhKeypair
{
    private DhKeypair(UInt128 privateKey, PublicKey publicKey)
    {
        Private = privateKey;
        Public = publicKey;
    }

    public UInt128 Private { get; }
    public PublicKey Public { get; }

    public static DhKeypair Generate()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var privateKey = BitConverter.ToUInt128(bytes) >> 1;
        if (privateKey < 2) privateKey = 2;
        return new DhKeypair(privateKey, new PublicKey(Dh.PowMod(Dh.G, privateKey)));
    }

    public SharedSecret Agree(PublicKey clientPublic)
    {
        if (clientPublic.IsDegenerate)
            throw SilverProtocolException.BadClientDhKey();

        return new SharedSecret(Dh.PowMod(clientPublic.Raw, Private));
    }
}
