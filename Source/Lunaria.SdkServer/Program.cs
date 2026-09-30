using System.Text.Json;
using System.Threading.RateLimiting;
using Lunaria.SdkServer;
using Lunaria.SdkServer.Persistence;
using Lunaria.SdkServer.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<SdkServerOptions>()
    .BindConfiguration("SdkServer")
    .Validate(o => o.Port is > 0 and <= 65535, "Port must be a valid TCP port")
    .ValidateOnStart();

var usePostgres = TryConfigurePostgres(
    new DbContextOptionsBuilder<SdkDbContext>(),
    builder.Configuration.GetSection("SdkServer:Database").Get<SdkServerOptions.DatabaseOptions>()
    ?? new SdkServerOptions.DatabaseOptions(),
    LoggerFactory.Create(b => b.AddConsole()));

builder.Services.AddDbContextFactory<SdkDbContext>((sp, dbOptions) => {
    var settings = sp.GetRequiredService<IOptions<SdkServerOptions>>().Value.Database;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    if (usePostgres)
    {
        dbOptions.UseNpgsql(settings.PostgresUrl).UseLoggerFactory(loggerFactory);
        return;
    }

    var sqlite = new SqliteConnectionStringBuilder {
        DataSource = settings.SqlitePath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    dbOptions
        .UseSqlite(sqlite)
        .UseLoggerFactory(loggerFactory);
});

builder.Services.AddSingleton<SdkCrypto>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<LastLoginTracker>();
builder.Services.AddSingleton<HotupdateStore>();

builder.Services.AddSingleton(sp => {
    var settings = sp.GetRequiredService<IOptions<SdkServerOptions>>().Value.Queue;
    return new QueueState(settings.MaxPlayers, (ulong)settings.TimeIntervalSeconds);
});
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddHttpClient("gateway");

builder.Services.AddControllersWithViews();

builder.Services.AddRateLimiter(limiter => {
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    limiter.OnRejected = async (context, _) => {
        context.HttpContext.Response.ContentType = "application/json";

        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new { code = 429, message = "Too many requests", ret = (string?)null }));
    };

    limiter.AddPolicy("login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ClientIp(context),
            _ => new SlidingWindowRateLimiterOptions {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(60),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));

    limiter.AddPolicy("risk", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ClientIp(context),
            _ => new SlidingWindowRateLimiterOptions {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(60),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));
});

static string ClientIp(HttpContext context)
{
    var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

    if (!string.IsNullOrEmpty(forwarded))
        return forwarded.Split(',')[0].Trim();

    if (context.Request.Headers.TryGetValue("X-Real-Ip", out var realIp))
        return realIp.ToString();

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static bool TryConfigurePostgres(
    DbContextOptionsBuilder<SdkDbContext> probe,
    SdkServerOptions.DatabaseOptions settings,
    ILoggerFactory loggerFactory
)
{
    try
    {
        probe.UseNpgsql(settings.PostgresUrl).UseLoggerFactory(loggerFactory);
        using var connection = new SdkDbContext(probe.Options);
        connection.Database.OpenConnection();
        connection.Database.EnsureCreated();
        return true;
    }
    catch (Exception)
    {
        return false;
    }
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SdkDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("SDK database ready ({Provider})", db.Database.ProviderName);
}

app.UseRateLimiter();

var hotupdate = app.Services.GetRequiredService<HotupdateStore>();
hotupdate.Scan();

app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(hotupdate.Root),
    RequestPath = "/hotupdate",
    ServeUnknownFileTypes = true
});

app.MapControllers();

var options = app.Services.GetRequiredService<IOptions<SdkServerOptions>>().Value;
app.Urls.Clear();
app.Urls.Add($"http://127.0.0.1:{options.Port}");

app.Logger.LogInformation("SDK server running on http://127.0.0.1:{Port}", options.Port);
app.Run();
