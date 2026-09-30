using System.Security.Cryptography;

namespace Lunaria.Silver;

/// <summary>
/// SilverNet handshake observed on the official server:
/// <code>
/// S->C 0x06 HELLO_INIT seq 0, session ID (client offset +0x3D0)
/// C->S 0x07 HELLO_RESP
/// S->C 0x08 HELLO_ACK seq 0 again, payload[1]=1 selects authentication
/// C->S 0x0A KEY_INIT starts DH after HELLO_ACK
/// S->C 0x0B KEX seq 1, blobs only
/// C->S 0x0C CLIENT_KEX echoes blobs, marker 0x1001, client public key A
/// S->C 0x0D KEY_REPLY seq 2, A echoed at [5:21], server public key B
/// C->S 0x0D KEY_ACK echoes the entire KEY_REPLY payload
/// S->C 0x01 NOTIFY seq 3, establish code (UDP port) and fresh ID
/// </code>
/// Retransmit KEX on timeout: the client drops it if it arrives before KEY_INIT.
/// </summary>
public sealed class ServerHandshake
{
    public const int MaxKexAttempts = 5;

    public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan KexAttemptTimeout = TimeSpan.FromSeconds(5);
    private readonly ulong _helloSessionId;
    private readonly SeqAllocator _seq = new();

    private readonly ushort _udpPort;
    private EstablishedSession? _established;
    private int _kexAttempts;

    private SessionBlob _kexBlob1;
    private SessionBlob _kexBlob2;
    private DhKeypair? _keypair;

    private Phase _phase = Phase.AwaitingHelloResp;
    private SharedSecret? _shared;

    public ServerHandshake(ushort udpPort)
    {
        _udpPort = udpPort;
        _helloSessionId = RandomU64();
    }

    public string PhaseName => _phase switch {
        Phase.AwaitingHelloResp => "await_hello_resp",
        Phase.AwaitingClientKeyInit => "await_client_key_init",
        Phase.AwaitingClientKeyExchange => "await_client_key_exchange",
        Phase.AwaitingKeyAck => "await_key_ack",
        Phase.Complete => "complete",
        _ => "unknown"
    };

    public TimeSpan CurrentTimeout =>
        _phase == Phase.AwaitingClientKeyExchange ? KexAttemptTimeout : StepTimeout;

    private static ulong RandomU64()
    {
        Span<byte> b = stackalloc byte[8];
        RandomNumberGenerator.Fill(b);
        return BitConverter.ToUInt64(b);
    }

    public Frame Start() =>
        Frame.Create(FrameType.HelloInit, HelloInit.Encode(_helloSessionId), _seq.Allocate());


    public Frame? OnFrame(Frame frame)
    {
        Incoming incoming;

        try
        {
            incoming = Incoming.Decode(frame);
        }
        catch (SilverDecodeException ex)
        {
            throw SilverProtocolException.Malformed($"malformed handshake frame: {ex.Detail}", ex);
        }

        if (incoming is Incoming.Heartbeat hb)
            return null; 

        switch (_phase, incoming)
        {
            case (Phase.AwaitingHelloResp, Incoming.HelloResp resp):
                _phase = Phase.AwaitingClientKeyInit;
                _seq.Reset(0);
                return Frame.Create(FrameType.HelloAck, HelloAck.Encode(), _seq.Allocate());

            case (Phase.AwaitingClientKeyInit, Incoming.ClientKeyInit): {
                _kexBlob1 = SessionBlob.Random();
                _kexBlob2 = SessionBlob.Random();
                _phase = Phase.AwaitingClientKeyExchange;
                _kexAttempts = 0;

                return Frame.Create(
                    FrameType.ServerKeyExchange,
                    ServerKeyExchange.Encode(_kexBlob1, _kexBlob2),
                    _seq.Allocate());
            }

            case (Phase.AwaitingClientKeyExchange, Incoming.ClientKeyExchange cke):
                return OnClientKeyExchange(cke);

            case (Phase.AwaitingKeyAck, Incoming.KeyAck ack):
                return OnKeyAck(ack);

            default:
                var got = frame.MessageType?.ToString() ?? $"0x{frame.Type:X2}";
                throw SilverProtocolException.UnexpectedFrame(PhaseName, ExpectedInPhase(_phase), got);
        }
    }

    private Frame? OnClientKeyExchange(Incoming.ClientKeyExchange cke)
    {
        if (!cke.Blob1.Bytes.AsSpan().SequenceEqual(_kexBlob1.Bytes) ||
            !cke.Blob2.Bytes.AsSpan().SequenceEqual(_kexBlob2.Bytes))
            throw SilverProtocolException.BlobEchoMismatch();

        if (cke.PublicKey.IsDegenerate)
            throw SilverProtocolException.BadClientDhKey();

        _keypair = DhKeypair.Generate();
        _shared = _keypair.Agree(cke.PublicKey);
        _phase = Phase.AwaitingKeyAck;

        return Frame.Create(
            FrameType.KeyReply,
            KeyReply.Encode(cke.PublicKey, _keypair.Public),
            _seq.Allocate());
    }

    private Frame? OnKeyAck(Incoming.KeyAck ack)
    {
        if (_keypair is null || _shared is null || ack.ServerKey != _keypair.Public)
            throw SilverProtocolException.KeyAckMismatch();

        var notifySessionId = RandomU64();
        _phase = Phase.Complete;

        _established = new EstablishedSession(
            new AesSession(_shared.Value),
            _helloSessionId,
            notifySessionId,
            _udpPort);

        return Frame.Create(
            FrameType.Notify,
            NotifyMessage.Encode(_udpPort, notifySessionId),
            _seq.Allocate());
    }

    public Frame? OnTimeout()
    {
        if (_phase == Phase.AwaitingClientKeyExchange)
        {
            if (_kexAttempts >= MaxKexAttempts)
                throw SilverProtocolException.KexRetriesExhausted(_kexAttempts);

            _kexAttempts++;

            return Frame.Create(
                FrameType.ServerKeyExchange,
                ServerKeyExchange.Encode(_kexBlob1, _kexBlob2),
                _seq.Allocate());
        }

        throw SilverProtocolException.Timeout(PhaseName);
    }

    public EstablishedSession? TakeEstablished()
    {
        if (_phase != Phase.Complete || _established is null)
            return null;

        var established = _established;
        _established = null;
        return established;
    }

    private static string ExpectedInPhase(Phase phase) => phase switch {
        Phase.AwaitingHelloResp => "HELLO_RESP (0x07)",
        Phase.AwaitingClientKeyInit => "CLIENT_KEY_INIT (0x0A)",
        Phase.AwaitingClientKeyExchange => "CLIENT_KEY_EXCHANGE (0x0C)",
        Phase.AwaitingKeyAck => "KEY_ACK (0x0D)",
        Phase.Complete => "nothing (handshake already complete)",
        _ => "unknown"
    };

    public static async Task<EstablishedSession> EstablishAsync(
        FrameReader reader,
        Stream writer,
        ushort udpPort,
        CancellationToken cancellationToken = default
    )
    {
        var handshake = new ServerHandshake(udpPort);
        await writer.WriteAsync(handshake.Start().Encode(), cancellationToken).ConfigureAwait(false);

        while (true)
        {
            if (handshake.TakeEstablished() is {} established)
                return established;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(handshake.CurrentTimeout);

            Frame? frame;

            try
            {
                frame = await reader.NextFrameAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (handshake.OnTimeout() is {} retry)
                {
                    await writer.WriteAsync(retry.Encode(), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                throw;
            }

            if (frame is null)
                throw SilverProtocolException.Closed();

            if (handshake.OnFrame(frame) is {} response)
                await writer.WriteAsync(response.Encode(), cancellationToken).ConfigureAwait(false);
        }
    }

    private enum Phase
    {
        AwaitingHelloResp,
        AwaitingClientKeyInit,
        AwaitingClientKeyExchange,
        AwaitingKeyAck,
        Complete
    }
}
