namespace Lunaria.Silver;


public readonly struct SharedSecret : IEquatable<SharedSecret>
{
    public UInt128 Raw { get; }

    public SharedSecret(UInt128 raw)
    {
        Raw = raw;
    }

    public byte[] ToLittleEndianBytes()
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes, Raw);
        return bytes;
    }

    public static bool operator ==(SharedSecret left, SharedSecret right) => left.Raw == right.Raw;
    public static bool operator !=(SharedSecret left, SharedSecret right) => left.Raw != right.Raw;

    public bool Equals(SharedSecret other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is SharedSecret other && Equals(other);
    public override int GetHashCode() => Raw.GetHashCode();
}
