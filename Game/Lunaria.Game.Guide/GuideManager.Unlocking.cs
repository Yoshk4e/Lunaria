namespace Lunaria.Game.Guide;

public sealed partial class GuideManager
{
    public bool Unlock(uint guideId, long unlockedAt)
    {
        if (!assets.Guides.Exists(guideId) || _entries.ContainsKey(guideId))
            return false;

        _entries[guideId] = new GuideEntry { UnlockedAt = unlockedAt, Read = false };
        IsDirty = true;
        return true;
    }

    public IReadOnlyList<uint> UnlockMany(IEnumerable<uint> guideIds, long unlockedAt) =>
        guideIds.Distinct().Where(id => Unlock(id, unlockedAt)).ToList();

    public IReadOnlyList<uint> MarkRead(IEnumerable<uint> guideIds)
    {
        var transitioned = new List<uint>();

        foreach (var id in guideIds.Distinct())
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.Read)
                continue;

            _entries[id] = entry with { Read = true };
            transitioned.Add(id);
        }

        if (transitioned.Count > 0)
            IsDirty = true;
        return transitioned;
    }

    public IReadOnlyList<uint> UnlockAll(long unlockedAt) => UnlockMany(assets.Guides.All, unlockedAt);
}
