using Isopoh.Cryptography.Argon2;

namespace Lunaria.SdkServer.Services;


public sealed class PasswordHasher
{
    private const int TimeCost = 3;
    private const int MemoryCost = 65536;
    private const int Parallelism = 4;
    private const int HashLength = 32;

    public string Hash(string password) =>
        Argon2.Hash(password, TimeCost, MemoryCost, Parallelism);

    public bool Verify(string password, string hash)
    {
        try
        {
            return Argon2.Verify(hash, password, secureArrayCall: null);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
