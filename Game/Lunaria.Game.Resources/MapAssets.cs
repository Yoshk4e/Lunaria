using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class MapAssets
{
    private const uint SavepointNpcType = 101;
    private const uint TeleportNpcType = 106;

    private const int OpenWorldType = 1;
    private const int WantedPosterModuleType = 31;
    private readonly FrozenDictionary<ulong, PFunctionalNPCTable> _functionalNpcs;

    private readonly FrozenDictionary<ulong, PMapDataTable> _maps;
    private readonly FrozenDictionary<ulong, PSavePointTemplateTable> _savepointTemplates;
    private readonly FrozenDictionary<ulong, (int X, int Y, int Z)> _spawns;

    public MapAssets(
        IReadOnlyDictionary<string, PMapDataTable> maps,
        IReadOnlyDictionary<string, PFunctionalNPCTable> functionalNpcs,
        IReadOnlyDictionary<string, PSavePointTemplateTable> savepointTemplates,
        IReadOnlyDictionary<string, PTeleportPointTemplateTable> teleports
    )
    {
        var mapLookup = new Dictionary<ulong, PMapDataTable>();
        var spawns = new Dictionary<ulong, (int X, int Y, int Z)>();

        foreach (var row in maps.Values)
        {
            if (row.BClientOnly != 0)
                continue;

            mapLookup[row.Id] = row;
            spawns[row.Id] = FVectorText.Parse(row.DefaultPos, "P_MapDataTable.json");
        }

        _maps = mapLookup.ToFrozenDictionary();
        _spawns = spawns.ToFrozenDictionary();

        var npcLookup = new Dictionary<ulong, PFunctionalNPCTable>();

        foreach (var row in functionalNpcs.Values)
        {
            npcLookup[row.Id] = row;
        }

        _functionalNpcs = npcLookup.ToFrozenDictionary();

        var templateLookup = new Dictionary<ulong, PSavePointTemplateTable>();

        foreach (var row in savepointTemplates.Values)
        {
            templateLookup[row.Id] = row;
        }

        _savepointTemplates = templateLookup.ToFrozenDictionary();

        if (_maps.Count == 0)
            throw new ResourceException("P_MapDataTable.json", "p_mapdatatable has no server-side maps");

        // Choose the lowest NPC ID with a default savepoint template. The client needs the NPC ID, not the template
        // ID.
        DefaultSavepoint = _functionalNpcs.Values
            .Where(npc => npc.Type == SavepointNpcType && _savepointTemplates.GetValueOrDefault(npc.TemplateId)?.BDefault == true)
            .Select(npc => npc.Id)
            .DefaultIfEmpty(0UL)
            .Min();

        if (DefaultSavepoint == 0)
            throw new ResourceException("P_FunctionalNPCTable.json",
                "no savepoint NPC on any map references a bDefault template");
    }

    public ulong DefaultSavepoint { get; }

    public int SavepointCount => _functionalNpcs.Values.Count(npc => npc.Type == SavepointNpcType);
    public int TeleportCount => _functionalNpcs.Values.Count(npc => npc.Type == TeleportNpcType);
    public int MapCount => _maps.Count;

    public bool MapExists(ulong mapId) => _maps.ContainsKey(mapId);

    public PMapDataTable? Map(ulong mapId) => _maps.GetValueOrDefault(mapId);

    public (int X, int Y, int Z)? SpawnPos(ulong mapId) =>
        _spawns.TryGetValue(mapId, out var pos) ? pos : null;

    public bool IsPlayable(ulong mapId) => _maps.GetValueOrDefault(mapId)?.BManipulateRole != 0;

    /// <summary>Scripted levels skip the map handshake, so their arrival objectives rely on client reports.</summary>
    public bool IsScriptedWorld(ulong mapId) => Map(mapId) is {} row && row.WorldType != OpenWorldType;

    /// <summary>ModuleType 31 is GameLuaModuleType.WantedPoster in the client's DataEnumTypeConfig.</summary>
    public bool IsWantedPosterMap(ulong mapId) => Map(mapId)?.ModuleType == WantedPosterModuleType;

    public ulong? SoleMapOfWorld(ulong worldId)
    {
        ulong? sole = null;

        foreach (var row in _maps.Values)
        {
            if (row.WorldId != worldId)
                continue;

            if (sole is not null)
                return null;

            sole = row.Id;
        }

        return sole;
    }

    public bool SavepointExists(ulong id) =>
        _functionalNpcs.TryGetValue(id, out var npc) && npc.Type == SavepointNpcType;

    public bool TeleportExists(ulong id) =>
        _functionalNpcs.TryGetValue(id, out var npc) && npc.Type == TeleportNpcType;

    public PFunctionalNPCTable? Savepoint(ulong id) =>
        _functionalNpcs.TryGetValue(id, out var npc) && npc.Type == SavepointNpcType ? npc : null;

    public PFunctionalNPCTable? Teleport(ulong id) =>
        _functionalNpcs.TryGetValue(id, out var npc) && npc.Type == TeleportNpcType ? npc : null;

    public (int X, int Y, int Z)? SavepointPos(ulong id)
    {
        var npc = Savepoint(id);

        if (npc is null)
            return null;

        var offset = FVectorText.Parse(
            _savepointTemplates.GetValueOrDefault(npc.TemplateId)?.LocationOffset ?? "",
            "P_SavePointTemplateTable.json");
        var pos = FVectorText.Parse(npc.Position, "P_FunctionalNPCTable.json");
        return (pos.X + offset.X, pos.Y + offset.Y, pos.Z + offset.Z);
    }
}
