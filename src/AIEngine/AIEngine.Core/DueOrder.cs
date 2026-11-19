namespace AIEngine;

/// <summary>Orders agents by the tick they are due, then by id, by ordinal comparison, so the order never depends on the culture of the machine.</summary>
internal sealed class DueOrder : IComparer<(long Due, string Id)>
{
    public static readonly DueOrder Instance = new();

    public int Compare((long Due, string Id) x, (long Due, string Id) y)
    {
        var byDue = x.Due.CompareTo(y.Due);
        return byDue != 0 ? byDue : string.CompareOrdinal(x.Id, y.Id);
    }
}