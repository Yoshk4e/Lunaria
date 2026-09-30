namespace Lunaria.Silver;

public abstract record Incoming
{
    public static Incoming Decode(Frame frame)
    {
        var payload = frame.Payload;

        switch (frame.MessageType)
        {
            case FrameType.HelloResp:
                return new HelloResp(payload);

            case FrameType.ClientKeyInit:
                if (payload.Length != Kex.PayloadLength)
                    throw new SilverDecodeException($"client key-init must be {Kex.PayloadLength} bytes, got {payload.Length}");

                return new ClientKeyInit(payload);

            case FrameType.ClientKeyExchange:
                return DecodeClientKeyExchange(payload);

            case FrameType.KeyReply:
                return DecodeKeyReply(payload);

            case FrameType.Heartbeat:
                if (payload.Length < 12)
                    throw new SilverDecodeException($"heartbeat payload needs 12 bytes, got {payload.Length}");

                return new Heartbeat(
                    BitConverter.ToUInt64(payload, startIndex: 0),
                    BitConverter.ToUInt32(payload, startIndex: 8));

            case FrameType.Data:
                return DecodeData(frame);

            default:
                return new Unknown(frame.Type);
        }
    }

    private static ClientKeyExchange DecodeClientKeyExchange(ReadOnlySpan<byte> p)
    {
        if (p.Length < Kex.MinLength)
            throw new SilverDecodeException($"key-exchange payload needs {Kex.MinLength} bytes, got {p.Length}");

        var marker = (ushort)(p[Kex.MarkerOffset] | p[Kex.MarkerOffset + 1] << 8);

        if (marker != Wire.ProtocolMarker)
            throw new SilverDecodeException($"protocol marker mismatch: expected 0x{Wire.ProtocolMarker:X4}, got 0x{marker:X4}");

        return new ClientKeyExchange(
            new SessionBlob(p.Slice(Kex.Blob1Offset, length: 16).ToArray()),
            new SessionBlob(p.Slice(Kex.Blob2Offset, length: 16).ToArray()),
            PublicKey.FromLittleEndian(p.Slice(Kex.PublicKeyOffset, length: 16)));
    }

    private static KeyAck DecodeKeyReply(ReadOnlySpan<byte> p)
    {
        if (p.Length < Kex.MinLength)
            throw new SilverDecodeException($"key-reply payload needs {Kex.MinLength} bytes, got {p.Length}");

        var marker = (ushort)(p[Kex.MarkerOffset] | p[Kex.MarkerOffset + 1] << 8);

        if (marker != Wire.ProtocolMarker)
            throw new SilverDecodeException($"protocol marker mismatch: expected 0x{Wire.ProtocolMarker:X4}, got 0x{marker:X4}");

        return new KeyAck(
            PublicKey.FromLittleEndian(p.Slice(Kex.Blob1Offset, length: 16)),
            PublicKey.FromLittleEndian(p.Slice(Kex.PublicKeyOffset, length: 16)));
    }


    private static Data DecodeData(Frame frame)
    {
        int a = frame.LengthA;
        int b = frame.LengthB;

        if (a % 16 != 0 || a > frame.Payload.Length || b > a)
            throw new SilverDecodeException(
                $"bad ciphertext lengths: a={frame.LengthA} b={frame.LengthB} payload={frame.Payload.Length}");

        return new Data(frame.LengthB, frame.Payload.AsSpan(start: 0, a).ToArray());
    }

    public sealed record HelloResp(byte[] Raw) : Incoming;

    public sealed record ClientKeyInit(byte[] Raw) : Incoming;

    public sealed record ClientKeyExchange(SessionBlob Blob1, SessionBlob Blob2, PublicKey PublicKey) : Incoming;

    public sealed record KeyAck(PublicKey EchoedClientKey, PublicKey ServerKey) : Incoming;

    public sealed record Heartbeat(ulong TimestampMs, uint Metric) : Incoming;

    public sealed record Data(ushort PlaintextLength, byte[] Ciphertext) : Incoming;

    public sealed record Unknown(byte Type) : Incoming;
}
