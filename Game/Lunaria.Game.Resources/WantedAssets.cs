using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public enum WantedNpcType
{
    StaminaExchange = 1,
    Shop = 2,
    Adventure = 3,
    RecoverHp = 4,
    Revive = 5,
    Settlement = 6,
    NormalBattle = 7,
    EndlessBattle = 8
}

public enum WantedEventType
{
    Battle = 1,
    Elite = 2,
    Boss = 3,
    Shop = 4,
    Branch = 5,
    Adventure = 6,
    Battle2 = 7,
    EndlessBattle = 8
}

public sealed class WantedAssets
{
    private readonly Dictionary<uint, SWantedPosterAwardTable> _awards = [];
    private readonly Dictionary<uint, PWPAdvContentTable> _adventureContents = [];
    private readonly Dictionary<uint, PWPAdvDialogTable> _adventureDialogs = [];
    private readonly Dictionary<uint, PWPAdvOptionTable> _adventureOptions = [];
    private readonly Dictionary<uint, PWPAdventureTable> _adventures = [];
    private readonly Dictionary<uint, PWPBlessShop> _blessGoods = [];
    private readonly Dictionary<uint, PWantedPosterBlessTable> _blesses = [];
    private readonly Dictionary<uint, PWantedPosterBlessBondTable> _bonds = [];
    private readonly Dictionary<string, uint> _config = [];
    private readonly Dictionary<uint, PWPCreatureShop> _creatureGoods = [];
    private readonly Dictionary<uint, PWantedPosterCreatureTable> _creatures = [];
    private readonly Dictionary<uint, PWantedPosterEffect> _effects = [];
    private readonly Dictionary<uint, PWantedPosterEntryTable> _entries = [];
    private readonly Dictionary<uint, PWantedPosterEventTable> _events = [];
    private readonly Dictionary<uint, PWantedPosterNPC> _npcs = [];

    private readonly Dictionary<uint, PWantedPosterTable> _posters = [];
    private readonly Dictionary<uint, PWPRelicShop> _relicGoods = [];
    private readonly Dictionary<uint, PWantedPosterRelicTable> _relics = [];
    private readonly List<PWantedPosterRevive> _reviveCosts = [];
    private readonly Dictionary<uint, uint> _routeSteps = [];
    private readonly Dictionary<(uint Kind, uint Group), List<uint>> _shopGoods = [];
    private readonly Dictionary<uint, PWantedPosterShop> _shops = [];
    private readonly Dictionary<(uint EntryId, uint StepCount), PWantedPosterStepCountTable> _stepCounts = [];
    private readonly Dictionary<(uint Route, uint Step), List<PWantedPosterProcessTable>> _steps = [];

    public WantedAssets(
        IReadOnlyDictionary<string, PWantedPosterTable> posters,
        IReadOnlyDictionary<string, PWantedPosterEntryTable> entries,
        IReadOnlyDictionary<string, PWantedPosterProcessTable> processes,
        IReadOnlyDictionary<string, PWantedPosterEventTable> events,
        IReadOnlyDictionary<string, PWantedPosterStepCountTable> stepCounts,
        IReadOnlyDictionary<string, PWantedPosterNPC> npcs,
        IReadOnlyDictionary<string, PWantedPosterBlessTable> blesses,
        IReadOnlyDictionary<string, PWantedPosterBlessBondTable> bonds,
        IReadOnlyDictionary<string, PWantedPosterRelicTable> relics,
        IReadOnlyDictionary<string, PWantedPosterCreatureTable> creatures,
        IReadOnlyDictionary<string, PWantedPosterShop> shops,
        IReadOnlyDictionary<string, PWPBlessShop> blessGoods,
        IReadOnlyDictionary<string, PWPRelicShop> relicGoods,
        IReadOnlyDictionary<string, PWPCreatureShop> creatureGoods,
        IReadOnlyDictionary<string, PWantedPosterRevive> revives,
        IReadOnlyDictionary<string, PWPAdventureTable> adventures,
        IReadOnlyDictionary<string, PWPAdvContentTable> adventureContents,
        IReadOnlyDictionary<string, PWPAdvDialogTable> adventureDialogs,
        IReadOnlyDictionary<string, PWPAdvOptionTable> adventureOptions,
        IReadOnlyDictionary<string, PWantedPosterConfig> config,
        IReadOnlyDictionary<string, PWantedPosterEffect> effects,
        ItemAssets items,
        IReadOnlyDictionary<string, SWantedPosterAwardTable> awards,
        WantedPolicy policy
    )
    {
        foreach (var row in awards.Values)
        {
            if (row.Probability > 100 || !Enum.IsDefined(row.Behavior))
                throw new ResourceException("S_WantedPosterAwardTable.json", $"invalid award {row.Id}");
            _awards.Add(row.Id, row);
        }
        foreach (var row in posters.Values)
        {
            _posters[row.Id] = row;
        }

        foreach (var row in entries.Values)
        {
            _entries[row.Id] = row;
        }

        foreach (var row in processes.Values)
        {
            if (!policy.EventPools.TryGetValue(row.Pool, out var pool) || pool.Length == 0)
                throw new ResourceException("gameplay-policy.json", $"missing event pool {row.Pool}");
            foreach (var eventId in pool)
                if (!events.Values.Any(e => e.Id == eventId))
                    throw new ResourceException("gameplay-policy.json", $"pool {row.Pool} references missing event {eventId}");
            foreach (var award in row.Award)
            {
                if (!_awards.ContainsKey(award))
                    throw new ResourceException("S_WantedPosterAwardTable.json", $"process {row.Id} references missing award {award}");
                if (!policy.Awards.ContainsKey(award))
                    throw new ResourceException("gameplay-policy.json", $"missing parameters for award {award}");
                if (_awards[award].Behavior == EWantedAwardType.ChangeHP)
                    throw new ResourceException("S_WantedPosterAwardTable.json", $"award {award}: ChangeHP parameters have not been recovered");
                var rule = policy.Awards[award];
                if (_awards[award].Behavior is EWantedAwardType.AddCoinTypeA or EWantedAwardType.AddCoinTypeB
                    && (rule.Amount == 0 || items.CurrencyItemFor((int)rule.Currency) is null))
                    throw new ResourceException("gameplay-policy.json", $"award {award} has invalid currency parameters");
            }
            (_steps.TryGetValue((row.RouteId, row.StepCount), out var list) ? list : _steps[(row.RouteId, row.StepCount)] = []).Add(row);
            _routeSteps[row.RouteId] = Math.Max(_routeSteps.GetValueOrDefault(row.RouteId), row.StepCount);
        }

        foreach (var row in events.Values)
        {
            _events[row.Id] = row;
        }

        foreach (var row in stepCounts.Values)
        {
            _stepCounts[(row.EntryId, row.StepCount)] = row;
        }

        foreach (var row in npcs.Values)
        {
            _npcs[row.Id] = row;
        }

        foreach (var row in blesses.Values)
        {
            _blesses[row.Id] = row;
        }

        foreach (var row in bonds.Values)
        {
            _bonds[row.Id] = row;
        }

        foreach (var row in relics.Values)
        {
            _relics[row.Id] = row;
        }

        foreach (var row in creatures.Values)
        {
            _creatures[row.Id] = row;
        }

        foreach (var row in shops.Values)
        {
            _shops[row.Id] = row;
        }

        foreach (var row in blessGoods.Values)
        {
            _blessGoods[row.Id] = row;

            if (!_shopGoods.TryGetValue(((uint)WantedPosterRandomRewardType.Bless, row.Group), out var list))
                list = _shopGoods[((uint)WantedPosterRandomRewardType.Bless, row.Group)] = [];
            list.Add(row.Id);
        }

        foreach (var row in relicGoods.Values)
        {
            _relicGoods[row.Id] = row;

            if (!_shopGoods.TryGetValue(((uint)WantedPosterRandomRewardType.Relic, row.Group), out var list))
                list = _shopGoods[((uint)WantedPosterRandomRewardType.Relic, row.Group)] = [];
            list.Add(row.Id);
        }

        foreach (var row in creatureGoods.Values)
        {
            _creatureGoods[row.Id] = row;

            if (!_shopGoods.TryGetValue(((uint)WantedPosterRandomRewardType.Creature, row.Group), out var list))
                list = _shopGoods[((uint)WantedPosterRandomRewardType.Creature, row.Group)] = [];
            list.Add(row.Id);
        }

        _reviveCosts.AddRange(revives.Values.OrderBy(row => row.Id));

        foreach (var row in adventures.Values)
        {
            _adventures[row.Id] = row;
        }

        foreach (var row in adventureContents.Values)
        {
            _adventureContents[row.Id] = row;
        }

        foreach (var row in adventureDialogs.Values)
        {
            _adventureDialogs[row.Id] = row;
        }

        foreach (var row in adventureOptions.Values)
        {
            _adventureOptions[row.Id] = row;
        }

        foreach (var row in config.Values)
        {
            if (decimal.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && value >= 0 && value <= uint.MaxValue && decimal.Truncate(value) == value)
                _config[row.Key] = (uint)value;
        }

        foreach (var row in effects.Values)
        {
            _effects[row.Id] = row;
        }

        if (_posters.Count == 0)
            throw new ResourceException("P_WantedPosterTable.json", "p_wantedpostertable has no rows");

        if (_entries.Count == 0)
            throw new ResourceException("P_WantedPosterEntryTable.json", "p_wantedposterentrytable has no rows");

        if (_steps.Count == 0)
            throw new ResourceException("P_WantedPosterProcessTable.json", "p_wantedposterprocesstable has no rows");

        if (_events.Count == 0)
            throw new ResourceException("P_WantedPosterEventTable.json", "p_wantedpostereventtable has no rows");

        // Pool IDs are not NPC IDs. The recovered tables omit pool membership.
        foreach (var entry in _entries.Values)
        {
            if (!_posters.ContainsKey(entry.WantedPosterId))
                throw new ResourceException(
                    "P_WantedPosterTable.json", $"entry {entry.Id} references missing poster {entry.WantedPosterId}");

            foreach (var route in new[] { entry.FristRouteId, entry.NormalRouteId })
            {
                if (route != 0 && !_routeSteps.ContainsKey(route))
                    throw new ResourceException(
                        "P_WantedPosterProcessTable.json", $"entry {entry.Id} references empty route {route}");
            }
        }
    }

    public IReadOnlyList<uint> AllPosters =>
        _entries.Values.Select(row => row.WantedPosterId).Distinct().Order().ToList();

    public IReadOnlyList<PWantedPosterRelicTable> AllRelics => _relics.Values.OrderBy(row => row.Id).ToList();

    public IReadOnlyList<PWantedPosterCreatureTable> AllCreatures =>
        _creatures.Values.OrderBy(row => row.Id).ToList();

    public uint CreatureMaxCount => _config.GetValueOrDefault("CreatureMaxCount", defaultValue: 4u);

    public uint ReviveMaxHp => _config.GetValueOrDefault("ReviveMaxHp", defaultValue: 10_000u);

    public PWantedPosterTable? Poster(uint id) => _posters.GetValueOrDefault(id);
    public SWantedPosterAwardTable? Award(uint id) => _awards.GetValueOrDefault(id);
    public IReadOnlyCollection<SWantedPosterAwardTable> Awards => _awards.Values;
    public IReadOnlyList<PWantedPosterBlessTable> AllBlesses => _blesses.Values.OrderBy(b => b.Id).ToArray();
    public PWantedPosterEntryTable? Entry(uint id) => _entries.GetValueOrDefault(id);
    public PWantedPosterEventTable? Event(uint id) => _events.GetValueOrDefault(id);
    public PWantedPosterNPC? Npc(uint id) => _npcs.GetValueOrDefault(id);
    public PWantedPosterBlessTable? Bless(uint id) => _blesses.GetValueOrDefault(id);
    public PWantedPosterRelicTable? Relic(uint id) => _relics.GetValueOrDefault(id);
    public PWantedPosterCreatureTable? Creature(uint id) => _creatures.GetValueOrDefault(id);
    public PWantedPosterShop? Shop(uint id) => _shops.GetValueOrDefault(id);
    public PWPBlessShop? BlessGood(uint id) => _blessGoods.GetValueOrDefault(id);
    public PWPRelicShop? RelicGood(uint id) => _relicGoods.GetValueOrDefault(id);
    public PWPCreatureShop? CreatureGood(uint id) => _creatureGoods.GetValueOrDefault(id);
    public PWPAdventureTable? Adventure(uint id) => _adventures.GetValueOrDefault(id);
    public PWPAdvContentTable? AdventureContent(uint id) => _adventureContents.GetValueOrDefault(id);
    public PWPAdvDialogTable? AdventureDialog(uint id) => _adventureDialogs.GetValueOrDefault(id);

    public bool TryAdvanceAdventure(uint adventureId, uint contentId, uint dialogId, out uint next)
    {
        next = 0;
        if (AdventureContent(contentId) is not {} content || content.AdventureId != adventureId) return false;

        if (content.Type == 2)
        {
            if (!content.DialogId.Contains(dialogId) || AdventureOption(dialogId) is not {} option) return false;

            next = option.SuccessNextContentId;
            return true;
        }
        if (content.Type != 1) return false;

        var current = content.DialogId.FirstOrDefault();
        var visited = new HashSet<uint>();

        while (current != 0 && visited.Add(current) && AdventureDialog(current) is {} dialog)
        {
            if (current == dialogId && dialog.NextId == 0)
            {
                next = content.NextDialogId.FirstOrDefault();
                return true;
            }
            current = dialog.NextId;
        }
        return false;
    }

    public PWPAdvOptionTable? AdventureOption(uint id) => _adventureOptions.GetValueOrDefault(id);
    public PWantedPosterStepCountTable? StepCount(uint entryId, uint step) => _stepCounts.GetValueOrDefault((entryId, step));
    public PWantedPosterEffect? Effect(uint id) => _effects.GetValueOrDefault(id);

    public IReadOnlyList<PWantedPosterEntryTable> EntriesOf(uint posterId) =>
        _entries.Values.Where(row => row.WantedPosterId == posterId).OrderBy(row => row.Difficulty).ToList();

    public IReadOnlyList<PWantedPosterProcessTable> StepsOf(uint routeId, uint step) =>
        _steps.GetValueOrDefault((routeId, step)) ?? [];

    public uint MaxStep(uint routeId) => _routeSteps.GetValueOrDefault(routeId);

    public IReadOnlyList<PWantedPosterEventTable> EventsOfType(uint wantedEventType) =>
        _events.Values.Where(row => row.WantedEventType == wantedEventType).OrderBy(row => row.Id).ToList();

    public IReadOnlyList<PWantedPosterBlessTable> BlessesOfType(uint blessType) =>
        _blesses.Values.Where(row => row.BlessType == blessType).OrderBy(row => row.Id).ToList();

    /// <summary>Cost of revive N, counting from 1. Null means no revives remain.</summary>
    public uint? ReviveCost(int reviveCount) =>
        reviveCount >= 1 && reviveCount <= _reviveCosts.Count ? _reviveCosts[reviveCount - 1].Type : null;

    public IReadOnlyList<uint> ShopGoods(uint shopKind, uint group) =>
        _shopGoods.GetValueOrDefault((shopKind, group)) ?? [];
}
