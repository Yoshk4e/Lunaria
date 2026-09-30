namespace Lunaria.QueryGateway;

public static class InstanceStateExtensions
{
    public static bool IsAvailable(this InstanceState state) =>
        state is InstanceState.Ready or InstanceState.Allocated;

    public static string WireName(this InstanceState state) => state.ToString();
}
