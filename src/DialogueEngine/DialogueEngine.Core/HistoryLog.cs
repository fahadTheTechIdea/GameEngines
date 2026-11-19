using System.Collections;

namespace DialogueEngine;

/// <summary>
/// The kept history as a ring: adding at the end and pushing the oldest out at the front cost the same however many entries are kept (a list would move every entry on each push), which matters
/// for a document that keeps a thousand. It grows as entries come, up to what the document keeps, and never shrinks.
/// </summary>
internal sealed class HistoryLog : IReadOnlyList<HistoryEntry>
{
    private HistoryEntry[] _items = new HistoryEntry[8];
    private int _head;

    public int Count { get; private set; }

    public HistoryEntry this[int index]
    {
        get => index >= 0 && index < Count ? _items[(_head + index) % _items.Length] : throw new IndexOutOfRangeException("history index " + index);
        set
        {
            if (index < 0 || index >= Count) throw new IndexOutOfRangeException("history index " + index);
            _items[(_head + index) % _items.Length] = value;
        }
    }

    private void Grow()
    {
        if (Count < _items.Length) return;
        var bigger = new HistoryEntry[_items.Length * 2];
        for (var i = 0; i < Count; i++) bigger[i] = _items[(_head + i) % _items.Length];
        _items = bigger;
        _head = 0;
    }

    /// <summary>Puts an entry after the newest.</summary>
    public void Add(HistoryEntry entry)
    {
        Grow();
        _items[(_head + Count) % _items.Length] = entry;
        Count++;
    }

    /// <summary>Takes out the oldest entry.</summary>
    public void RemoveFirst()
    {
        if (Count == 0) throw new InvalidOperationException("the history is empty");
        _items[_head] = null!;
        _head = (_head + 1) % _items.Length;
        Count--;
    }

    /// <summary>Puts an entry before the oldest (taking a push-out back).</summary>
    public void InsertFirst(HistoryEntry entry)
    {
        Grow();
        _head = (_head - 1 + _items.Length) % _items.Length;
        _items[_head] = entry;
        Count++;
    }

    /// <summary>Takes out the newest entry.</summary>
    public void RemoveLast()
    {
        if (Count == 0) throw new InvalidOperationException("the history is empty");
        _items[(_head + Count - 1) % _items.Length] = null!;
        Count--;
    }

    public IEnumerator<HistoryEntry> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return _items[(_head + i) % _items.Length];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
