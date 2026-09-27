namespace RecompOne.Runtime.Assets.Native;

// Owner-thread cache. Eviction releases ownership, never mutates a value still
// referenced by a queued frame. One oversized item may exceed the budget.
public sealed class ResidentCache<TKey, TValue>(long budget, Action<TValue>? release = null) where TKey : notnull
{
    private sealed record Entry(TKey Key, TValue Value, long Bytes);
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _order = [];
    public long Budget { get; } = budget > 0 ? budget : throw new ArgumentOutOfRangeException(nameof(budget));
    public long Bytes { get; private set; }
    public long PeakBytes { get; private set; }
    public long Evictions { get; private set; }
    public int Count => _entries.Count;
    public bool TryGet(TKey key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out var node)) { value = default!; return false; }
        if (node != _order.Last) { _order.Remove(node); _order.AddLast(node); }
        value = node.Value.Value;
        return true;
    }
    public void Add(TKey key, TValue value, long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (_entries.ContainsKey(key)) throw new InvalidOperationException("Duplicate cache key");
        while (_order.First is {} first && (bytes > Budget || Bytes > Budget - bytes)) Remove(first);
        var node = _order.AddLast(new Entry(key, value, bytes));
        _entries.Add(key, node); Bytes += bytes; PeakBytes = Math.Max(PeakBytes, Bytes);
    }
    private void Remove(LinkedListNode<Entry> node)
    {
        _order.Remove(node); _entries.Remove(node.Value.Key);
        Bytes -= node.Value.Bytes; Evictions++; release?.Invoke(node.Value.Value);
    }
    public void Clear() { while (_order.First is {} first) Remove(first); }
    public static long BudgetFromEnvironment(string name, int defaultMiB) =>
        (long)(int.TryParse(Environment.GetEnvironmentVariable(name), out int value)
            ? Math.Clamp(value, 16, 1024) : defaultMiB) * 1024 * 1024;
}
