namespace Lunaria.Silver;


public sealed record PingRequest
{
    private PingRequest(byte[] timestamp)
    {
        Timestamp = timestamp;
    }

    public byte[] Timestamp { get; }

    public static PingRequest Decode(ReadOnlySpan<byte> payload)
    {
        var timestamp = new byte[8];

        if (payload.Length >= 16)
            payload.Slice(start: 8, length: 8).CopyTo(timestamp);
        return new PingRequest(timestamp);
    }
}
