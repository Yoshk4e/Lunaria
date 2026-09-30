namespace Lunaria.SdkServer;

public sealed class SdkServerOptions
{
    public int Port { get; set; } = 10010;

    public string GatewayUrl { get; set; } = "http://127.0.0.1:10020";

    public int TokenTtlSeconds { get; set; } = 3600;

    public string HotupdateManifestPath { get; set; } = "hotupdate_manifest.json";

    public string HotupdateDir { get; set; } = "hotupdate";

    public QueueOptions Queue { get; set; } = new();

    public DatabaseOptions Database { get; set; } = new();

    public sealed class QueueOptions
    {
        public int MaxPlayers { get; set; } = 10000;

        public int TimeIntervalSeconds { get; set; } = 5;
    }

    public sealed class DatabaseOptions
    {

        public string PostgresUrl { get; set; } = "postgres://lunaria:1337@localhost:5432/lunaria";

        public int MaxConnections { get; set; } = 20;

        public int ConnectTimeoutSeconds { get; set; } = 3;

        public string SqlitePath { get; set; } = "sdk_server.db";
    }
}
