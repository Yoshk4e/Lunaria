namespace Lunaria.GameServer;


public sealed class GameServerOptions
{
    public string GatewayUrl { get; set; } = "http://127.0.0.1:10020";

    public string ServerIp { get; set; } = "127.0.0.1";

    public string ServerRegion { get; set; } = "local";

    public int Port { get; set; } = 30000;

    public int ScenePort { get; set; } = 30002;

    public int MaxPlayers { get; set; } = 2000;

    public int MaxPendingHandshakes { get; set; } = 1024;
    public int HandshakeTimeoutSeconds { get; set; } = 30;

    public int ListenBacklog { get; set; } = 2048;
    public int AcceptLoops { get; set; } = 4;

    public int SessionQueueDepth { get; set; } = 256;

    public string AssetsDir { get; set; } = "assets";

    public string DatabasePath { get; set; } = "game_server.db";
}
