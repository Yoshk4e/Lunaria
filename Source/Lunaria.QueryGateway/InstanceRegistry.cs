using System.Collections.Concurrent;

namespace Lunaria.QueryGateway;

public sealed class InstanceRegistry(QueryGatewayOptions options)
{
    private readonly ConcurrentDictionary<string, GameInstance> _instances = [];

    private readonly Lock _regionGate = new();
    private readonly ConcurrentDictionary<string, List<string>> _regionIndex = [];
    private readonly ConcurrentDictionary<string, long> _roundRobin = [];

    public string Register(RegisterRequest request)
    {
        var instanceId = Guid.CreateVersion7().ToString();

        var instance = new GameInstance(
            instanceId,
            request.Region,
            request.Ip,
            request.Port,
            request.Addrs,
            InstanceState.Ready,
            PlayerCount: 0,
            request.MaxPlayers,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _instances[instanceId] = instance;

        lock (_regionGate)
        {
            _regionIndex.GetOrAdd(request.Region, _ => []).Add(instanceId);
        }
        return instanceId;
    }

    public bool Heartbeat(HeartbeatRequest request)
    {
        if (!_instances.TryGetValue(request.InstanceId, out var instance))
            return false;

        _instances[request.InstanceId] = instance with {
            State = request.State,
            PlayerCount = request.PlayerCount,
            LastHeartbeatUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        return true;
    }

    public AllocateResponse? Allocate(string region)
    {
        RemoveStale();

        if (!_regionIndex.TryGetValue(region, out var ids))
            return null;

        var available = _instances.Values
            .Where(i => ids.Contains(i.Id) && i.State.IsAvailable())
            .ToList();

        if (available.Count == 0)
            return null;

        var tick = _roundRobin.AddOrUpdate(region, addValue: 1, (_, current) => current + 1);
        var selected = available[(int)(tick % available.Count)];
        return ToAllocateResponse(selected);
    }

    public AllocateResponse? AllocateLocal()
    {
        RemoveStale();

        var selected = _instances.Values
            .Where(i => i.State.IsAvailable() && (i.Ip == "127.0.0.1" || i.Ip == "::1"))
            .FirstOrDefault();
        return selected is null ? null : ToAllocateResponse(selected);
    }

    private static AllocateResponse ToAllocateResponse(GameInstance instance) =>
        new(instance.Id, instance.Ip, instance.Port, instance.Addrs);

    public int RemoveStale()
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - options.StaleTimeoutSeconds;

        var staleIds = _instances
            .Where(kv => kv.Value.LastHeartbeatUnixSeconds < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in staleIds)
        {
            _instances.TryRemove(id, out _);
        }

        if (staleIds.Count > 0)
        {
            foreach (var ids in _regionIndex.Values)
            {
                ids.RemoveAll(staleIds.Contains);
            }
        }

        return staleIds.Count;
    }

    public IReadOnlyList<InstanceSummary> AllAvailable()
    {
        RemoveStale();

        return _instances.Values
            .Where(i => i.State.IsAvailable())
            .Select(i => new InstanceSummary(
                i.Id, i.Region, i.Ip, i.Port, i.Addrs, i.State.WireName(), i.PlayerCount, i.MaxPlayers))
            .ToList();
    }
}
