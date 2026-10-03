using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Lunaria.Common.Tracking;

public class TrackedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, ITrackedState
    where TKey : notnull
{
    private readonly IDictionary<TKey, TValue> _items;
    private readonly Dictionary<TKey, (ChangeJournal Journal, Action Handler)> _children = [];
    public TrackedDictionary() : this(new Dictionary<TKey, TValue>()) { }
    public TrackedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items) : this() { foreach (var p in items) Add(p.Key, p.Value); Changes.AcceptAll(); }
    protected TrackedDictionary(IDictionary<TKey, TValue> items) => _items = items;
    [JsonIgnore] public ChangeJournal Changes { get; } = new();
    public TValue this[TKey key] { get => _items[key]; set { Before(key); Detach(key); _items[key] = value; Attach(key, value); } }
    public ICollection<TKey> Keys => _items.Keys;
    public ICollection<TValue> Values => _items.Values;
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public void Add(TKey key, TValue value) { if (_items.ContainsKey(key)) throw new ArgumentException("Duplicate key.", nameof(key)); Before(key); _items.Add(key, value); Attach(key, value); }
    public bool TryAdd(TKey key, TValue value) { if (ContainsKey(key)) return false; Add(key, value); return true; }
    public bool Remove(TKey key) { if (!_items.ContainsKey(key)) return false; Before(key); Detach(key); return _items.Remove(key); }
    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value) { if (!TryGetValue(key, out value)) return false; return Remove(key); }
    public void Clear() { foreach (var key in Keys.ToArray()) Remove(key); }
    public bool ContainsKey(TKey key) => _items.ContainsKey(key);
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _items.TryGetValue(key, out value);
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public bool Contains(KeyValuePair<TKey, TValue> item) => _items.Contains(item);
    public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    private void Before(TKey key) => Changes.Record(key,
        () => StateValue.Capture(_items.TryGetValue(key, out var v) ? (true, v) : (false, default(TValue))),
        () => _items.TryGetValue(key, out var value) && value is ITrackedState child ? child.Changes.Capture() : null);
    private void Attach(TKey key, TValue value)
    {
        if (value is not ITrackedState child) return;
        Action handler = () => Before(key);
        _children.Add(key, (child.Changes, handler));
        child.Changes.Changing += handler;
    }
    private void Detach(TKey key)
    {
        if (_children.Remove(key, out var child)) child.Journal.Changing -= child.Handler;
    }
}

public sealed class TrackedSortedDictionary<TKey, TValue> : TrackedDictionary<TKey, TValue> where TKey : notnull
{
    public TrackedSortedDictionary() : base(new SortedDictionary<TKey, TValue>()) { }
    public TrackedSortedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items) : this() { foreach (var p in items) Add(p.Key, p.Value); Changes.AcceptAll(); }
}
