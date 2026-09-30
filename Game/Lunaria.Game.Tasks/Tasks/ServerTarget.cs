using System.Globalization;
using Lunaria.Game.Resources;

namespace Lunaria.Game.Tasks;

public enum ServerTarget
{
    None = 0,
    GiveItems = 1,
    TakeItems = 2,
    UseItem = 3,
    ArriveMap = 4,
    TeamLevel = 6,
    OpenCase = 7,
    GiveClue = 8,
    GiveEvidence = 9,
    DecryptEvidence = 10,
    OwnHouse = 14,
    StartTask = 15,
    CompleteCaseStage = 18,
    CompleteTask = 20,
    WantedEvent = 21,
    MapState = 23,
    /// <summary>Wait for a minute of day, with 1440 meaning midnight. This is not an elapsed duration.</summary>
    ReachGameTime = 26,
    OwnItem = 27,
    CompleteDungeon = 28,
    ResetMonster = 31,
    CompleteBattle = 33
}

public static class TargetParameter
{
    public static bool TryId(string text, out ulong id)
    {
        id = 0;

        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || value < 0 || value > ulong.MaxValue || decimal.Truncate(value) != value)
            return false;

        id = (ulong)value;
        return true;
    }

    public static bool TryCount(string text, out uint count)
    {
        count = 0;

        if (!TryId(text, out var value) || value > uint.MaxValue)
            return false;

        count = (uint)value;
        return true;
    }

    public static bool TryItems(string text, out IReadOnlyList<ItemGrant> items)
    {
        items = [];
        var totals = new Dictionary<uint, uint>();

        foreach (var token in text.Split('|'))
        {
            var pair = token.Split('#');

            if (pair.Length != 2 || !TryCount(pair[0], out var id) || id == 0
                || !TryCount(pair[1], out var count) || count == 0
                || (ulong)totals.GetValueOrDefault(id) + count > uint.MaxValue)
                return false;

            totals[id] = totals.GetValueOrDefault(id) + count;
        }
        items = totals.Select(p => new ItemGrant(p.Key, p.Value)).ToArray();
        return items.Count > 0;
    }
}
