using Lunaria.Common.Tracking;
using Lunaria.Game.Player.Persistence;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed partial class RoleManager : TrackedObject
{
    public const int MaxRoles = (int)EnmSizeLimit.MaxRoleNumOneAccount;

    private readonly TrackedList<RoleState> __tracked_roles = new(r => r.Id);
    [Tracked] private partial TrackedList<RoleState> _roles { get; }

    [Untracked]
    private long? _active;

    public bool IsEmpty => _roles.Count == 0;
    public int Count => _roles.Count;
    public bool AtCap => _roles.Count >= MaxRoles;

    public IReadOnlyList<RoleState> All => _roles;

    public bool HasActive => _active is not null;

    public void Load(IEnumerable<RoleRow> rows)
    {
        _roles.Clear();
        _roles.AddRange(rows.Select(RoleState.FromRow).OrderBy(r => r.Id));
        AcceptLoadedState();
        _active = null;
    }

    public RoleState Adopt(RoleRow row)
    {
        var state = RoleState.FromRow(row);
        _roles.Add(state);
        _roles.Changes.AcceptKey(state.Id);
        return state;
    }

    public long NextSlot() => _roles.Count;

    /// <summary>The client uses SCAccountLogin.brief_role as its single-entry role list.</summary>
    public RoleState? Newest() => _roles.Count > 0 ? _roles[^1] : null;

    public RoleState? Get(long roleId) => _roles.FirstOrDefault(r => r.Id == roleId);

    public bool SetActive(long roleId)
    {
        if (!_roles.Any(r => r.Id == roleId))
            return false;

        _active = roleId;
        return true;
    }

    public RoleState? Active() => _active is {} id ? Get(id) : null;

    public IReadOnlyList<RoleState> DirtyRoles() =>
        _roles.Changes.ChangedKeys.Cast<long>().Select(Get).OfType<RoleState>().ToList();

    public void MarkPersisted(long roleId) => _roles.Changes.AcceptKey(roleId);

    private void Replace(RoleState role)
    {
        var index = _roles.FindIndex(r => r.Id == role.Id);

        if (index < 0)
            return;

        _roles[index] = role;
    }
}
