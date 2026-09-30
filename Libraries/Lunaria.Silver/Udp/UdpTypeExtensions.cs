namespace Lunaria.Silver;


public static class UdpTypeExtensions
{
    public static UdpType? FromByte(byte value) => value switch {
        (byte)UdpType.Connect => UdpType.Connect,
        (byte)UdpType.Data => UdpType.Data,
        (byte)UdpType.Ping => UdpType.Ping,
        (byte)UdpType.Pong => UdpType.Pong,
        _ => null
    };
}
