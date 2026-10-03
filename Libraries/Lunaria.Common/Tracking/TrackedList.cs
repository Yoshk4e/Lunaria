using System.Collections;
using System.Text.Json.Serialization;

namespace Lunaria.Common.Tracking;

/// <summary>A key selector tracks entries by stable ID. Without one, changes include the whole list and its order.</summary>
public sealed class TrackedList<T> : IList<T>, IReadOnlyList<T>, ITrackedState
{
    private readonly List<T> _items = [];
    private readonly Dictionary<ChangeJournal, (int Count, Action Handler)> _children = [];
    private readonly Func<T, object>? _key;
    public TrackedList() { }
    public TrackedList(Func<T, object> key) => _key = key;
    public TrackedList(IEnumerable<T> items) { foreach (var item in items) { _items.Add(item); Attach(item); } }
    [JsonIgnore] public ChangeJournal Changes { get; } = new();
    public T this[int index] { get => _items[index]; set { var old = _items[index]; Before(old); Before(value); Detach(old); _items[index] = value; Attach(value); } }
    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public void Add(T item) { Before(item); _items.Add(item); Attach(item); }
    public void AddRange(IEnumerable<T> items) { foreach (var item in items.ToArray()) Add(item); }
    public void Insert(int index, T item) { if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index)); Before(item); _items.Insert(index, item); Attach(item); }
    public bool Remove(T item) { var index = IndexOf(item); if (index < 0) return false; RemoveAt(index); return true; }
    public void RemoveAt(int index) { var item = _items[index]; Before(item); _items.RemoveAt(index); Detach(item); }
    public int RemoveAll(Predicate<T> match) { var count = 0; for (var i = Count - 1; i >= 0; i--) if (match(_items[i])) { RemoveAt(i); count++; } return count; }
    public void Clear() { if (Count == 0) return; foreach (var item in _items) { Before(item); Detach(item); } _items.Clear(); }
    public bool Contains(T item) => _items.Contains(item);
    public int IndexOf(T item) => _items.IndexOf(item);
    public int FindIndex(Predicate<T> match) => _items.FindIndex(match);
    public T? Find(Predicate<T> match) => _items.Find(match);
    public void Sort(Comparison<T> comparison) { if (_key is null) Before(default!); _items.Sort(comparison); }
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    private void Before(T item)
    {
        if (_key is null)
        {
            Changes.Record("items", () => StateValue.Capture(_items), () => {
                var children = _children.Keys.Select(c => c.Capture()).ToArray();
                return new ChangeBatch(() => { foreach (var child in children) child.Accept(); });
            });
            return;
        }
        var key = _key(item);
        Changes.Record(key, () => {
            var index = _items.FindIndex(value => Equals(_key(value), key));
            return StateValue.Capture(index >= 0 ? (true, _items[index]) : (false, default(T)));
        }, () => _items.FirstOrDefault(value => Equals(_key(value), key)) is ITrackedState child ? child.Changes.Capture() : null);
    }
    private void Attach(T item)
    {
        if (item is not ITrackedState state) return;
        if (_children.TryGetValue(state.Changes, out var child)) { _children[state.Changes] = (child.Count + 1, child.Handler); return; }
        Action handler = () => Before(item);
        _children.Add(state.Changes, (1, handler));
        state.Changes.Changing += handler;
    }
    private void Detach(T item)
    {
        if (item is not ITrackedState state || !_children.TryGetValue(state.Changes, out var child)) return;
        if (child.Count > 1) { _children[state.Changes] = (child.Count - 1, child.Handler); return; }
        state.Changes.Changing -= child.Handler;
        _children.Remove(state.Changes);
    }
}
