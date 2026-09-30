namespace Lunaria.Silver;

public readonly struct PublicKey : IEquatable<PublicKey>
{
    public UInt128 Raw { get; }

    public PublicKey(UInt128 raw)
    {
        Raw = raw;
    }

    public static PublicKey FromLittleEndian(ReadOnlySpan<byte> bytes)
        => new(BitConverter.ToUInt128(bytes));

    public void WriteLittleEndian(Span<byte> destination)
        => BitConverter.TryWriteBytes(destination, Raw);

    public bool IsDegenerate => Raw < 2 || Raw >= Dh.P - 1;

    public static bool operator ==(PublicKey left, PublicKey right) => left.Raw == right.Raw;
    public static bool operator !=(PublicKey left, PublicKey right) => left.Raw != right.Raw;

    public bool Equals(PublicKey other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is PublicKey other && Equals(other);
    public override int GetHashCode() => Raw.GetHashCode();
    public override string ToString() => $"{Raw:x32}";
}
