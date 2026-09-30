namespace Lunaria.Silver;

public sealed class SilverProtocolException : Exception
{
    public SilverProtocolException(SilverProtocolFailure reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    private SilverProtocolException(SilverProtocolFailure reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }

    public SilverProtocolFailure Reason { get; }

    public string? Phase { get; init; }

    public string? Expected { get; init; }

    public string? Got { get; init; }

    public static SilverProtocolException Timeout(string phase) =>
        new(SilverProtocolFailure.Timeout, $"handshake timed out in phase '{phase}'") { Phase = phase };

    public static SilverProtocolException Closed() =>
        new(SilverProtocolFailure.Closed, "peer closed the connection");

    public static SilverProtocolException UnexpectedFrame(string phase, string expected, string got) =>
        new(SilverProtocolFailure.UnexpectedFrame,
            $"unexpected frame in phase '{phase}': expected {expected}, got {got}") {
            Phase = phase,
            Expected = expected,
            Got = got
        };

    public static SilverProtocolException Malformed(string message, Exception inner) =>
        new(SilverProtocolFailure.Malformed, message, inner);

    public static SilverProtocolException BadClientDhKey() =>
        new(SilverProtocolFailure.BadClientDhKey, "client DH public key is degenerate (0, 1 or p-1)");

    public static SilverProtocolException BlobEchoMismatch() =>
        new(SilverProtocolFailure.BlobEchoMismatch, "client key-exchange blobs did not echo ours");

    public static SilverProtocolException KeyAckMismatch() =>
        new(SilverProtocolFailure.KeyAckMismatch, "client key-ack did not round-trip our public key");

    public static SilverProtocolException KexRetriesExhausted(int attempts) =>
        new(SilverProtocolFailure.KexRetriesExhausted, $"key-exchange retries exhausted after {attempts} attempts");

    public static SilverProtocolException IdleTimeout(TimeSpan idle) =>
        new(SilverProtocolFailure.IdleTimeout, $"session idle for {idle.TotalSeconds:0}s, closing");

    public static SilverProtocolException Kicked() =>
        new(SilverProtocolFailure.Kicked, "session taken over by a newer login");
}
