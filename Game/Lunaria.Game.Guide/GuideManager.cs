using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Guide;

public sealed partial class GuideManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Guide");

    private readonly TrackedSortedDictionary<uint, GuideEntry> __tracked_entries = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, GuideEntry> _entries { get; }

    public IReadOnlyDictionary<uint, GuideEntry> Entries => _entries;

    public int Count => _entries.Count;
    public bool IsEmpty => _entries.Count == 0;

    public int CatalogueSize => assets.Guides.Count;

    public void Load(IEnumerable<KeyValuePair<uint, GuideEntry>> persisted)
    {
        _entries.Clear();

        foreach (var (guideId, entry) in persisted)
        {
            if (assets.Guides.Exists(guideId))
                _entries[guideId] = entry;
        }
        AcceptLoadedState();
    }

    public GuideEntry? Entry(uint guideId) => _entries.GetValueOrDefault(guideId);

    public bool IsUnlocked(uint guideId) => _entries.ContainsKey(guideId);

    public IReadOnlyList<GuideInfo> Infos() =>
        _entries.Select(kv => ToGuideInfo(kv.Key, kv.Value)).ToList();

    public IReadOnlyList<GuideInfo> InfosOf(IEnumerable<uint> guideIds) =>
        guideIds.Distinct()
            .Where(_entries.ContainsKey)
            .Order()
            .Select(id => ToGuideInfo(id, _entries[id]))
            .ToList();

    public GuideInfo ToGuideInfo(uint guideId, GuideEntry entry) => new() {
        GuideId = guideId,
        State = entry.Read ? EnmGuideState.Readed : EnmGuideState.Unlock,
        UnlockTime = entry.UnlockedAt
    };
}
