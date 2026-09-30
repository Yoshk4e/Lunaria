using System.Security.Cryptography;
using System.Text;

namespace Lunaria.SdkServer.Services;


public sealed class SdkCrypto
{
    private const string KeyText = "vs0U9Jo1Iz3TpEfnTORHm7Eh";
    private const string IvText = "1aORFg5tC2AT5MBM"; 
    private readonly byte[] _iv = Encoding.ASCII.GetBytes(IvText);

    private readonly byte[] _key = Encoding.ASCII.GetBytes(KeyText);

    public string Decrypt(string data)
    {
        byte[] buffer;

        try
        {
            buffer = Convert.FromBase64String(data);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Base64 decode error");
        }

        byte[] plaintext;

        try
        {
            plaintext = AesCbc(buffer, encrypt: false);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("Decryption failed");
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    public string Encrypt(string plaintext)
    {
        var ciphertext = AesCbc(Encoding.UTF8.GetBytes(plaintext), encrypt: true);
        return Convert.ToBase64String(ciphertext);
    }

    private byte[] AesCbc(byte[] input, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;
        aes.IV = _iv;

        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transform.TransformFinalBlock(input, inputOffset: 0, input.Length);
    }
}
