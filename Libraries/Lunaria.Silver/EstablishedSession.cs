namespace Lunaria.Silver;

public sealed record EstablishedSession(
    AesSession Aes,
    ulong HelloSessionId,
    ulong NotifySessionId,
    ushort UdpPort
);
