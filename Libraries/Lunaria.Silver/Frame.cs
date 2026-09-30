using System.Diagnostics.CodeAnalysis;

namespace Lunaria.Silver;

/// <summary>
/// SilverNet TCP frame layout, little-endian:
/// <code>
/// [0:2]   reserved, 00 00
/// [2]     message type
/// [3:5]   u16 length A: payload length, or ciphertext length for encrypted DATA
/// [5:7]   u16 length B: client key-exchange payload length, or DATA plaintext length
/// [7:11]  u32 sequence number
/// [11:]   payload
/// </code>
/// </summary>
public sealed class Frame
{
    public const int HeaderLength = 11;
    public const int MaxFrameLength = HeaderLength + ushort.MaxValue;

    private Frame(byte type, ushort lengthA, ushort lengthB, uint sequence, byte[] payload)
    {
        Type = type;
        LengthA = lengthA;
        LengthB = lengthB;
        Sequence = sequence;
        Payload = payload;
    }

    public byte Type { get; }
    public ushort LengthA { get; }
    public ushort LengthB { get; }
    public uint Sequence { get; }
    public byte[] Payload { get; }

    public FrameType? MessageType => FrameTypeExtensions.FromByte(Type);

    public static Frame Create(FrameType type, ReadOnlySpan<byte> payload, uint sequence) =>
        new((byte)type, checked((ushort)payload.Length), lengthB: 0, sequence, payload.ToArray());

    public static Frame EncryptedData(ReadOnlySpan<byte> ciphertext, ushort plaintextLength, uint sequence) =>
        new((byte)FrameType.Data, checked((ushort)ciphertext.Length), plaintextLength, sequence, ciphertext.ToArray());

    public static Frame CreateRaw(byte type, ushort lengthA, ushort lengthB, uint sequence, byte[] payload) =>
        new(type, lengthA, lengthB, sequence, payload);

    public byte[] Encode()
    {
        var output = new byte[HeaderLength + Payload.Length];
        output[2] = Type;
        output[3] = (byte)LengthA;
        output[4] = (byte)(LengthA >> 8);
        output[5] = (byte)LengthB;
        output[6] = (byte)(LengthB >> 8);
        output[7] = (byte)Sequence;
        output[8] = (byte)(Sequence >> 8);
        output[9] = (byte)(Sequence >> 16);
        output[10] = (byte)(Sequence >> 24);
        Payload.CopyTo(output, HeaderLength);
        return output;
    }

    public static int? FrameLengthOf(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderLength)
            return null;

        var type = buffer[2];
        var lengthA = (ushort)(buffer[3] | buffer[4] << 8);
        var lengthB = (ushort)(buffer[5] | buffer[6] << 8);
        return HeaderLength + EffectivePayloadLength(type, lengthA, lengthB);
    }

    private static ushort EffectivePayloadLength(byte type, ushort lengthA, ushort lengthB)
    {
        var isClientKex = FrameTypeExtensions.FromByte(type) is FrameType.ClientKeyInit
            or FrameType.ClientKeyExchange
            or FrameType.KeyReply;
        return isClientKex && lengthB > lengthA ? lengthB : lengthA;
    }

    public static bool TryDecode(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out Frame? frame)
    {
        frame = null;
        var total = FrameLengthOf(bytes);

        if (total is null || bytes.Length < total.Value)
            return false;

        var type = bytes[2];
        var lengthA = (ushort)(bytes[3] | bytes[4] << 8);
        var lengthB = (ushort)(bytes[5] | bytes[6] << 8);
        var sequence = BitConverter.ToUInt32(bytes.Slice(start: 7, length: 4));
        var payload = bytes.Slice(HeaderLength, total.Value - HeaderLength).ToArray();
        frame = new Frame(type, lengthA, lengthB, sequence, payload);
        return true;
    }
}
