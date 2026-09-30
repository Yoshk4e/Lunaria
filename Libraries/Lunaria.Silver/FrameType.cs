namespace Lunaria.Silver;

public enum FrameType : byte
{
    Notify = 0x01,
    Data = 0x02,
    Heartbeat = 0x03,
    HelloInit = 0x06,
    HelloResp = 0x07,
    HelloAck = 0x08,
    ClientKeyInit = 0x0A,
    ServerKeyExchange = 0x0B,
    ClientKeyExchange = 0x0C,
    KeyReply = 0x0D
}
