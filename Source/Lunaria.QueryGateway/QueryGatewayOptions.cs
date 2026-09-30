namespace Lunaria.QueryGateway;

public sealed class QueryGatewayOptions
{
    public int Port { get; set; } = 10020;
    public int StaleTimeoutSeconds { get; set; } = 30;
}
