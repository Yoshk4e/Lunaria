using System.Collections;
using System.Text.Json.Serialization;

namespace Lunaria.Common.Tracking;

public sealed class TrackedSet<T> : ISet<T>, IReadOnlySet<T>, ITrackedState where T : notnull
{
    private readonly SortedSet<T> _items = [];
    public TrackedSet() { }
    public TrackedSet(IEnumerable<T> items) => _items.UnionWith(items);
    [JsonIgnore] public ChangeJournal Changes { get; } = new();
    public int Count => _items.Count;
    public T? Min => _items.Min;
    public bool IsReadOnly => false;
    public bool Add(T item) { if (Contains(item)) return false; Before(item); return _items.Add(item); }
    void ICollection<T>.Add(T item) => Add(item);
    public bool Remove(T item) { if (!Contains(item)) return false; Before(item); return _items.Remove(item); }
    public void Clear() { foreach (var item in _items.ToArray()) Remove(item); }
    public bool Contains(T item) => _items.Contains(item);
    public int RemoveWhere(Predicate<T> match) { var items = _items.Where(i => match(i)).ToArray(); foreach (var item in items) Remove(item); return items.Length; }
    public void UnionWith(IEnumerable<T> other) { foreach (var item in other.ToArray()) Add(item); }
    public void ExceptWith(IEnumerable<T> other) { foreach (var item in other.ToArray()) Remove(item); }
    public void IntersectWith(IEnumerable<T> other) { var keep = other.ToHashSet(); foreach (var item in _items.ToArray()) if (!keep.Contains(item)) Remove(item); }
    public void SymmetricExceptWith(IEnumerable<T> other) { foreach (var item in other.Distinct().ToArray()) if (!Remove(item)) Add(item); }
    public bool IsSubsetOf(IEnumerable<T> other) => _items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _items.IsSupersetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _items.IsProperSupersetOf(other);
    public bool IsProperSubsetOf(IEnumerable<T> other) => _items.IsProperSubsetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _items.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _items.SetEquals(other);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    private void Before(T item) => Changes.Record(item, () => StateValue.Capture(_items.Contains(item)));
}
