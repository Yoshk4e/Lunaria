namespace Lunaria.Silver;

public enum SilverProtocolFailure
{
    Io,
    Closed,
    Timeout,
    UnexpectedFrame,
    Malformed,
    BadClientDhKey,
    BlobEchoMismatch,
    KeyAckMismatch,
    KexRetriesExhausted,
    IdleTimeout,
    Kicked
}
