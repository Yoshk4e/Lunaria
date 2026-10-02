using Lunaria.Game.Logging;
using Lunaria.GameServer.Logging;
using Lunaria.GameServer.Services;
using Lunaria.Game.Player.Auth;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.GameServer;
using Lunaria.GameServer.Gateway;
using Lunaria.GameServer.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

if (args.Length > 0 && args[0] == "--validate-content")
{
    var warnings = ContentValidator.Validate(args.Length > 1 ? args[1] : "assets");
    foreach (var warning in warnings) Console.WriteLine($"WARNING: {warning}");
    Console.WriteLine($"Content validation: {warnings.Count} warning(s). Warnings are advisory.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new StylishConsoleLoggerProvider());
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("LUNARIA_");
builder.Services
    .AddOptions<GameServerOptions>()
    .BindConfiguration("GameServer")
    .Validate(o => o.Port is > 0 and <= 65535, "Port must be a valid TCP port")
    .Validate(o => o.ScenePort is > 0 and <= 65535, "ScenePort must be a valid TCP port")
    .Validate(o => o.MaxPlayers > 0, "MaxPlayers must be positive")
    .Validate(o => o.MaxPendingHandshakes > 0, "MaxPendingHandshakes must be positive")
    .Validate(o => o.HandshakeTimeoutSeconds > 0, "HandshakeTimeoutSeconds must be positive")
    .Validate(o => o.ListenBacklog > 0, "ListenBacklog must be positive")
    .Validate(o => o.AcceptLoops > 0, "AcceptLoops must be positive")
    .Validate(o => o.SessionQueueDepth > 0, "SessionQueueDepth must be positive")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => new GameData(
    sp.GetRequiredService<IOptions<GameServerOptions>>().Value.AssetsDir,
    sp.GetRequiredService<ILogger<GameData>>()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<GameData>());

builder.Services.AddDbContextFactory<GameDbContext>((sp, dbOptions) => {
    var settings = sp.GetRequiredService<IOptions<GameServerOptions>>().Value;

    var connectionString = new SqliteConnectionStringBuilder {
        DataSource = settings.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    dbOptions
        .UseSqlite(connectionString)
        .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
});

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GameServerOptions>>().Value);
builder.Services.AddSingleton<GameServerRuntime>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Router>();
builder.Services.AddSingleton<TrustAllAuthenticator>();
builder.Services.AddSingleton<IAuthenticator>(sp => sp.GetRequiredService<TrustAllAuthenticator>());
builder.Services.AddSingleton<UdpSessionRegistry>();
builder.Services.AddSingleton<UdpListenerService>();
builder.Services.AddSingleton<ConnectionGate>();
builder.Services.AddSingleton<GameServerMetrics>();
builder.Services.AddSingleton<ClientSession>();
builder.Services.AddSingleton<SessionClock>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SessionClock>());
builder.Services.AddSingleton<AccountRepository>();
builder.Services.AddSingleton<RoleRepository>();
builder.Services.AddSingleton<CharacterRepository>();
builder.Services.AddSingleton<MotiveRepository>();
builder.Services.AddSingleton<GuideRepository>();
builder.Services.AddSingleton<MailRepository>();
builder.Services.AddSingleton<RoleSaveRepository>();
builder.Services.AddSingleton<RoleStateStore>();
builder.Services.AddSingleton<RoleSessionService>();

builder.Services.AddHttpClient("gateway");
builder.Services.AddHostedService<BootstrapService>();
builder.Services.AddHostedService<GatewayRegistrationService>();

var host = builder.Build();
GameLog.Configure(host.Services.GetRequiredService<ILoggerFactory>());

await host.RunAsync().ConfigureAwait(false);
