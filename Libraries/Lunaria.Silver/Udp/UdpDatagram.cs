namespace Lunaria.Silver;


public sealed record UdpDatagram
{
    public const int HeaderLength = 14;
    public const int ConnectPayloadLength = 75;
    public const int PingPayloadLength = 24;
    public const int ResultCodeOffset = 71;

    public byte Type { get; init; }
    public ulong SessionId { get; init; }
    public uint LengthField { get; init; }

    public byte[] Payload { get; init; } = [];

    public static UdpDatagram? Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderLength)
            return null;

        return new UdpDatagram {
            Type = buffer[0],
            SessionId = BitConverter.ToUInt64(buffer.Slice(start: 2, length: 8)),
            LengthField = BitConverter.ToUInt32(buffer.Slice(start: 10, length: 4)),
            Payload = buffer.Slice(HeaderLength).ToArray()
        };
    }

    public byte[] EncodeServerResponse()
    {
        var frame = new byte[HeaderLength + Payload.Length];
        frame[0] = Type;
        Payload.CopyTo(frame, HeaderLength);
        return frame;
    }
}
