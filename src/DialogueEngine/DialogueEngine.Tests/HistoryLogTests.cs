using DialogueEngine;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The ring that keeps the history behaves exactly like a list that pushes from the front, over thousands of random operations, including across its growth and wrap-around.</summary>
public sealed class HistoryLogTests
{
    private static HistoryEntry Entry(long turn) => new(turn, turn, HistoryKind.Line, "c", "n", "s", "k" + turn, [], "", false, false, false);

    [Fact]
    public void ItMatchesAListUnderRandomOperations()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var random = new Random(seed);
            var ring = new HistoryLog();
            var model = new List<HistoryEntry>();
            long next = 0;
            for (var step = 0; step < 2_000; step++)
            {
                switch (random.Next(5))
                {
                    case 0: case 1: { var e = Entry(next++); ring.Add(e); model.Add(e); break; }
                    case 2: if (model.Count > 0) { ring.RemoveFirst(); model.RemoveAt(0); } break;
                    case 3: { var e = Entry(next++); ring.InsertFirst(e); model.Insert(0, e); break; }
                    default: if (model.Count > 0) { ring.RemoveLast(); model.RemoveAt(model.Count - 1); } break;
                }
                if (model.Count > 0 && random.Next(7) == 0)
                {
                    var at = random.Next(model.Count);
                    var e = Entry(next++);
                    ring[at] = e;
                    model[at] = e;
                }
                Assert.Equal(model.Count, ring.Count);
                Assert.True(model.SequenceEqual(ring), $"seed {seed} step {step}");
                if (model.Count > 0) Assert.Same(model[^1], ring[ring.Count - 1]);
            }
        }
    }

    [Fact]
    public void ItRefusesAnIndexOutsideItsEntriesAndAnEmptyRemoval()
    {
        var ring = new HistoryLog();
        Assert.Throws<IndexOutOfRangeException>(() => ring[0]);
        Assert.Throws<InvalidOperationException>(() => ring.RemoveFirst());
        Assert.Throws<InvalidOperationException>(() => ring.RemoveLast());
        ring.Add(Entry(1));
        Assert.Throws<IndexOutOfRangeException>(() => ring[1]);
        Assert.Throws<IndexOutOfRangeException>(() => ring[-1] = Entry(2));
    }

    [Fact]
    public void ItGrowsPastItsFirstCapacityWithEntriesInOrder()
    {
        var ring = new HistoryLog();
        for (var i = 0; i < 100; i++) ring.Add(Entry(i));
        for (var i = 0; i < 50; i++) ring.RemoveFirst();
        for (var i = 100; i < 300; i++) ring.Add(Entry(i));
        Assert.Equal(Enumerable.Range(50, 250).Select(i => (long)i), ring.Select(e => e.Turn));
    }
}
