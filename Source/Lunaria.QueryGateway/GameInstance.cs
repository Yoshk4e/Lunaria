namespace Lunaria.QueryGateway;

public sealed record GameInstance(
    string Id,
    string Region,
    string Ip,
    int Port,
    List<ServerAddr> Addrs,
    InstanceState State,
    int PlayerCount,
    int MaxPlayers,
    long LastHeartbeatUnixSeconds
);
