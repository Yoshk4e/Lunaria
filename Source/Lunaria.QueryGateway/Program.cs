using System.Text.Json.Serialization;
using Lunaria.QueryGateway;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<QueryGatewayOptions>()
    .BindConfiguration("QueryGateway")
    .Validate(o => o.Port is > 0 and <= 65535, "Port must be a valid TCP port")
    .ValidateOnStart();

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<QueryGatewayOptions>>().Value);
builder.Services.AddSingleton<InstanceRegistry>();
builder.Services.AddHostedService<GatewayCleanupService>();

builder.Services.ConfigureHttpJsonOptions(json => {
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddControllers().AddJsonOptions(json => { json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

var app = builder.Build();

app.MapControllers();

var options = app.Services.GetRequiredService<IOptions<QueryGatewayOptions>>().Value;
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{options.Port}");

app.Logger.LogInformation("query gateway listening on 0.0.0.0:{Port}", options.Port);
app.Run();
