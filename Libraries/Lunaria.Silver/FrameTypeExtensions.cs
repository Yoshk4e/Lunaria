namespace Lunaria.Silver;

public static class FrameTypeExtensions
{
    public static FrameType? FromByte(byte value) => value switch {
        (byte)FrameType.Notify => FrameType.Notify,
        (byte)FrameType.Data => FrameType.Data,
        (byte)FrameType.Heartbeat => FrameType.Heartbeat,
        (byte)FrameType.HelloInit => FrameType.HelloInit,
        (byte)FrameType.HelloResp => FrameType.HelloResp,
        (byte)FrameType.HelloAck => FrameType.HelloAck,
        (byte)FrameType.ClientKeyInit => FrameType.ClientKeyInit,
        (byte)FrameType.ServerKeyExchange => FrameType.ServerKeyExchange,
        (byte)FrameType.ClientKeyExchange => FrameType.ClientKeyExchange,
        (byte)FrameType.KeyReply => FrameType.KeyReply,
        _ => null
    };
}
