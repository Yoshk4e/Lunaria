namespace Lunaria.Silver;


public sealed record PongResponse
{
    public PongResponse(byte[] timestamp)
    {
        Timestamp = timestamp;
    }

    public byte[] Timestamp { get; }

    public byte[] Encode()
    {
        var payload = new byte[UdpDatagram.PingPayloadLength];
        Timestamp.CopyTo(payload.AsSpan(start: 16, length: 8));
        return payload;
    }
}
