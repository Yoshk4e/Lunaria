namespace Lunaria.Silver;

/// <summary>
/// CONNECT request payload contains two 16-byte blobs at [5:21] and [21:37].
/// UDP header, little-endian:
/// <code>
/// [0]     message type (1 CONNECT, 3 PING, 4 PONG)
/// [1]     reserved
/// [2:10]  u64 session ID
/// [10:14] u32 peer-supplied length
/// [14:]   payload
/// </code>
/// </summary>
public sealed record ConnectRequest(byte[] Blob1, byte[] Blob2)
{
    public static ConnectRequest? Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 37)
            return null;

        return new ConnectRequest(
            payload.Slice(start: 5, length: 16).ToArray(),
            payload.Slice(start: 21, length: 16).ToArray());
    }
}
