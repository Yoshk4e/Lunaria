using Lunaria.Game.Player.Persistence;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed partial class RoleManager
{
    public const int MaxRoles = (int)EnmSizeLimit.MaxRoleNumOneAccount;

    private readonly HashSet<long> _dirty = [];

    private readonly List<RoleState> _roles = [];

    private long? _active;

    public bool IsDirty => _dirty.Count > 0;

    public bool IsEmpty => _roles.Count == 0;
    public int Count => _roles.Count;
    public bool AtCap => _roles.Count >= MaxRoles;

    public IReadOnlyList<RoleState> All => _roles;

    public bool HasActive => _active is not null;

    public void Load(IEnumerable<RoleRow> rows)
    {
        _roles.Clear();
        _roles.AddRange(rows.Select(RoleState.FromRow).OrderBy(r => r.Id));
        _dirty.Clear();
        _active = null;
    }

    public RoleState Adopt(RoleRow row)
    {
        var state = RoleState.FromRow(row);
        _roles.Add(state);
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
        _roles.Where(r => _dirty.Contains(r.Id)).ToList();

    public void MarkPersisted(long roleId) => _dirty.Remove(roleId);

    private void Replace(RoleState role)
    {
        var index = _roles.FindIndex(r => r.Id == role.Id);

        if (index < 0)
            return;

        _roles[index] = role;
        _dirty.Add(role.Id);
    }
}
