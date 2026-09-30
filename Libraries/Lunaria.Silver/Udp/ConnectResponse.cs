namespace Lunaria.Silver;

/// <summary>
/// The 75-byte reply is zero except for a little-endian u32 result at offset 71. Code 0 establishes the connection.
/// </summary>
public sealed record ConnectResponse(uint ResultCode)
{
    public byte[] Encode()
    {
        var payload = new byte[UdpDatagram.ConnectPayloadLength];
        BitConverter.TryWriteBytes(payload.AsSpan(UdpDatagram.ResultCodeOffset, length: 4), ResultCode);
        return payload;
    }
}
