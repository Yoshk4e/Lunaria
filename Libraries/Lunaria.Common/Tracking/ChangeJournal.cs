using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunaria.Common.Tracking;

/// <summary>Captures each value before its first change. Used by one session event loop.</summary>
public sealed class ChangeJournal
{
    private readonly Dictionary<object, Entry> _entries = [];
    private readonly Dictionary<ChangeJournal, Action> _subscriptions = [];
    private readonly HashSet<ChangeJournal> _pendingChildren = [];
    private readonly Dictionary<string, ChangeJournal> _namedChildren = [];
    private readonly HashSet<ChangeJournal> _parents = [];
    public Func<bool>? PreserveLoadedChanges { get; set; }
    internal bool LoadsAreMutations => PreserveLoadedChanges?.Invoke() ?? _parents.Any(p => p.LoadsAreMutations);
    public event Action? Changing;

    public bool HasChanges => _entries.Values.Any(e => !e.Original.Equals(e.Read()))
                              || _pendingChildren.Any(c => c.HasChanges);

    public IReadOnlyList<object> ChangedKeys => _entries.Where(p => !p.Value.Original.Equals(p.Value.Read()))
        .Select(p => p.Key).ToArray();

    public bool IsChanged(string name) => (_entries.TryGetValue(name, out var entry) && !entry.Original.Equals(entry.Read()))
        || (_namedChildren.TryGetValue(name, out var child) && child.HasChanges);

    public void AcceptKey(object key) => _entries.Remove(key);

    /// <summary>Require a save even when gameplay state has not changed, such as after a format upgrade.</summary>
    public void Invalidate(string reason)
    {
        Changing?.Invoke();
        _entries[reason] = new Entry(StateValue.Capture(false), () => StateValue.Capture(true));
    }

    internal void Record(object key, Func<StateValue> read, Func<ChangeBatch?>? captureRelated = null)
    {
        // Notify parents before the value changes so they can capture the original nested state.
        Changing?.Invoke();
        if (!_entries.ContainsKey(key)) _entries.Add(key, new Entry(read(), read, captureRelated));
    }

    public void Observe(ChangeJournal child, string? name = null)
    {
        if (name is not null) _namedChildren[name] = child;
        if (ReferenceEquals(this, child) || _subscriptions.ContainsKey(child)) return;
        Action changed = () => { Changing?.Invoke(); _pendingChildren.Add(child); };
        _subscriptions.Add(child, changed);
        child._parents.Add(this);
        child.Changing += changed;
        if (child.HasChanges) _pendingChildren.Add(child);
    }

    public void Forget(ChangeJournal child)
    {
        if (_subscriptions.Remove(child, out var changed)) { child.Changing -= changed; child._parents.Remove(this); }
        _pendingChildren.Remove(child);
        foreach (var name in _namedChildren.Where(p => ReferenceEquals(p.Value, child)).Select(p => p.Key).ToArray())
            _namedChildren.Remove(name);
    }

    /// <summary>Capture the state being saved. Accept the batch after commit to preserve later changes.</summary>
    public ChangeBatch Capture()
    {
        var entries = _entries.Select(p => (p.Key, p.Value, Current: p.Value.Read(), Related: p.Value.CaptureRelated?.Invoke())).ToArray();
        var children = _pendingChildren.Select(c => c.Capture()).ToArray();
        return new ChangeBatch(() => {
            foreach (var (key, entry, current, related) in entries)
            {
                related?.Accept();
                if (!_entries.TryGetValue(key, out var live) || !ReferenceEquals(live, entry)) continue;
                entry.Original = current;
                if (current.Equals(entry.Read())) _entries.Remove(key);
            }
            foreach (var child in children) child.Accept();
            _pendingChildren.RemoveWhere(c => !c.HasChanges);
        });
    }

    /// <summary>Mark the current state as saved. Use a captured batch when saving across an await.</summary>
    public void AcceptAll() => Capture().Accept();

    private sealed class Entry(StateValue original, Func<StateValue> read, Func<ChangeBatch?>? captureRelated = null)
    {
        public StateValue Original = original;
        public Func<StateValue> Read { get; } = read;
        public Func<ChangeBatch?>? CaptureRelated { get; } = captureRelated;
    }
}

public sealed class ChangeBatch(Action accept)
{
    private Action? _accept = accept;
    public void Accept() => Interlocked.Exchange(ref _accept, null)?.Invoke();
}

internal sealed class StateValue(byte[] bytes) : IEquatable<StateValue>
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    public static StateValue Capture<T>(T value) => new(JsonSerializer.SerializeToUtf8Bytes(value, Options));
    public bool Equals(StateValue? other) => other is not null && bytes.AsSpan().SequenceEqual(other.Bytes);
    private byte[] Bytes => bytes;
    public override bool Equals(object? obj) => obj is StateValue other && Equals(other);
    public override int GetHashCode() { var hash = new HashCode(); hash.AddBytes(bytes); return hash.ToHashCode(); }
}

public interface ITrackedState
{
    ChangeJournal Changes { get; }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class TrackedAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class TrackChildrenAttribute : Attribute;

/// <summary>Exclude this member from persistence tracking.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class UntrackedAttribute : Attribute;

public abstract class TrackedObject : ITrackedState
{
    [JsonIgnore] public ChangeJournal Changes { get; } = new();
    [JsonIgnore] public bool IsDirty => Changes.HasChanges;
    public void ClearDirty() => Changes.AcceptAll();

    /// <summary>
    /// Accept loaded state when no role is active. Active players accept all state after database loading,
    /// so later loads and resets during gameplay remain tracked changes.
    /// </summary>
    protected void AcceptLoadedState() { if (!Changes.LoadsAreMutations) Changes.AcceptAll(); }

    protected T Observe<T>(T value, string? name = null)
    {
        if (value is ITrackedState child) Changes.Observe(child.Changes, name);
        return value;
    }

    protected void SetTracked<T>(ref T field, T value, string name, Func<T> read)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        Changes.Record(name, () => StateValue.Capture(read()));
        if (field is ITrackedState old) Changes.Forget(old.Changes);
        field = value;
        Observe(value, name);
    }
}
