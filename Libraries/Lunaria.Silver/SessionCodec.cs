using System.Diagnostics;

namespace Lunaria.Silver;


public sealed class SessionCodec
{
    private readonly SeqAllocator _seq;
    private readonly long _startedAtTimestamp = Stopwatch.GetTimestamp();

    public SessionCodec(AesSession aes)
        : this(aes, nextSeq: 4)
    {}

    public SessionCodec(AesSession aes, uint nextSeq)
    {
        Aes = aes;
        _seq = new SeqAllocator(nextSeq);
    }

    public AesSession Aes { get; }

    public byte[] DataFrame(ReadOnlySpan<byte> plaintext)
    {
        var encrypted = Aes.Encrypt(plaintext);
        return encrypted.ToFrame(_seq.Allocate()).Encode();
    }

    public byte[] HeartbeatFrame()
    {
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uptimeMicros = Stopwatch.GetTimestamp() - _startedAtTimestamp;
        var payload = HeartbeatMessage.Now(unixMs, uptimeMicros);
        return Frame.Create(FrameType.Heartbeat, payload, _seq.Allocate()).Encode();
    }
}
