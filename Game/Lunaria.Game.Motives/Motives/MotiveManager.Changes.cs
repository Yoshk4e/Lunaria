using Lunaria.Common.Tracking;
using Msg;

namespace Lunaria.Game.Motives;

public sealed partial class MotiveManager : TrackedObject
{
    [Untracked]
    private readonly Dictionary<ulong, CmdItem> _changed = [];

    public CmdItem ToInventoryItem(MotiveState state, bool removed = false, bool isNew = false) => new() {
        ItemId = state.ItemId,
        BindId = state.UniqId,
        ItemNum = removed ? 0u : 1u,
        IsNew = isNew,
        MotiveData = ToCmdMotiveData(state)
    };

    private void MarkChanged(MotiveState state, bool removed = false, bool isNew = false) =>
        _changed[state.UniqId] = ToInventoryItem(state, removed, isNew);

    public IReadOnlyList<CmdItem> DrainInventoryChanges()
    {
        var result = _changed.Values.ToArray();
        _changed.Clear();
        return result;
    }
}
