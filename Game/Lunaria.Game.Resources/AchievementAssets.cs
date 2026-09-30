using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class AchievementAssets
{
    private readonly Dictionary<uint, PAchievementTable> _byFinishEvent = [];
    private readonly Dictionary<uint, PGlobalEventFinishTable> _events = [];
    private readonly Dictionary<uint, PAchievementTable> _rows = [];

    public AchievementAssets(
        IReadOnlyDictionary<string, PAchievementTable> rows,
        IReadOnlyDictionary<string, PGlobalEventFinishTable> events
    )
    {
        foreach (var row in rows.Values)
        {
            _rows[row.Id] = row;
            _byFinishEvent[row.FinishId] = row;
        }

        foreach (var row in events.Values)
        {
            _events[row.Id] = row;
        }

        if (_rows.Count == 0)
            throw new ResourceException("P_AchievementTable.json", "p_achievementtable has no rows");

        foreach (var row in _rows.Values)
        {
            if (!_events.TryGetValue(row.FinishId, out var @event) || @event.NeedCount == 0)
                throw new ResourceException(
                    "P_GlobalEventFinishTable.json", $"p_achievementtable {row.Id} references invalid event {row.FinishId}");
            // Achievement drop 1000 comes from S_DropTable, not the fixed-drop table.
        }
    }

    public IReadOnlyList<PAchievementTable> All => _rows.Values.OrderBy(row => row.Id).ToList();

    public PAchievementTable? Get(uint id) => _rows.GetValueOrDefault(id);
    public PAchievementTable? ByFinishEvent(uint eventId) => _byFinishEvent.GetValueOrDefault(eventId);
    public uint NeedCount(uint eventId) => _events.GetValueOrDefault(eventId)?.NeedCount ?? 0;
}
