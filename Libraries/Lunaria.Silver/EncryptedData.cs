namespace Lunaria.Silver;


public readonly record struct EncryptedData(byte[] Ciphertext, ushort PlaintextLength)
{
    public Frame ToFrame(uint sequence) =>
        Frame.EncryptedData(Ciphertext, PlaintextLength, sequence);
}
