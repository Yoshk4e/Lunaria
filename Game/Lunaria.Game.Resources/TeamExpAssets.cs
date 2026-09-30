using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class TeamExpAssets
{
    private readonly FrozenDictionary<uint, uint> _expByReason;

    public TeamExpAssets(IReadOnlyDictionary<string, PTeamExpAwardTable> rows)
    {
        var expByReason = new Dictionary<uint, uint>();

        foreach (var row in rows.Values)
        {
            expByReason[row.Reason] = row.TeamExp;
        }

        if (expByReason.Count == 0)
            throw new ResourceException("P_TeamExpAwardTable.json", "p_teamexpawardtable has no rows");

        _expByReason = expByReason.ToFrozenDictionary();
    }

    public uint TeamExpFor(uint reason) =>
        _expByReason.TryGetValue(reason, out var exp) ? exp : 0;
}
