using System.Security.Cryptography;

namespace Lunaria.Silver;

// Encrypted DATA uses field A for ciphertext length and field B for plaintext length. A must be a multiple of 16.
public sealed class AesSession
{
    private readonly ICryptoTransform _decryptor;
    private readonly ICryptoTransform _encryptor;

    public AesSession(SharedSecret secret)
        : this(secret.ToLittleEndianBytes())
    {}

    public AesSession(byte[] keyBytes)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = keyBytes;
        _encryptor = aes.CreateEncryptor();
        _decryptor = aes.CreateDecryptor();
        KeyHex = Convert.ToHexString(keyBytes);
    }

    public string KeyHex { get; }

    public EncryptedData Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var plaintextLength = (ushort)Math.Min(plaintext.Length, ushort.MaxValue);
        var paddedLength = (plaintextLength + 15) / 16 * 16;
        var output = new byte[paddedLength];
        plaintext.Slice(start: 0, plaintextLength).CopyTo(output);

        _encryptor.TransformBlock(output, inputOffset: 0, paddedLength, output, outputOffset: 0);
        return new EncryptedData(output, plaintextLength);
    }

    public byte[] Decrypt(byte[] data)
    {
        var output = (byte[])data.Clone();
        var blocks = output.Length / 16;

        if (blocks > 0)
            _decryptor.TransformBlock(output, inputOffset: 0, blocks * 16, output, outputOffset: 0);
        return output;
    }
}
