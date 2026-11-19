using AIEngine;
using Xunit;

namespace AIEngine.Tests;

/// <summary>The two indexes of what an agent knows always agree.</summary>
public sealed class PerceptStoreTests
{
    private static readonly PerceptKindDef Kind = new(0, "stock", [new ValueDef(0, "held", ScenarioEngine.Contracts.FactKind.Integer, 0, 100)], [Val.Of(0)], 8, 4, 10, owned: true);

    private static Percept Of(string id, string owner) => new(Kind, id, owner, 0, [Val.Of(1)]);

    [Fact]
    public void AnOwnerWithNothingLeftHoldsNothingNotAnEmptySet()
    {
        var store = new PerceptStore();
        store.Add(Of("a", "x"));
        store.Add(Of("b", "x"));
        store.Add(Of("c", "y"));
        Assert.Equal(2, store.OwnedBy("x")!.Count);
        Assert.True(store.Remove("a"));
        Assert.Single(store.OwnedBy("x")!);
        Assert.True(store.Remove("b"));
        Assert.Null(store.OwnedBy("x"));
        Assert.Equal(1, store.Count);
        Assert.False(store.Remove("b"));
        Assert.Equal(["c"], store.OwnedBy("y")!.Select(p => p.Id));
        Assert.Equal(["c"], store.Values.Select(p => p.Id));
    }

    [Fact]
    public void ThingsWithoutAnOwnerAreInTheOrderedIndexOnly()
    {
        var store = new PerceptStore();
        store.Add(Of("b", ""));
        store.Add(Of("a", ""));
        Assert.Equal(["a", "b"], store.Values.Select(p => p.Id));
        Assert.Null(store.OwnedBy(""));
        Assert.True(store.TryGetValue("a", out var found) && found.Id == "a");
        Assert.False(store.TryGetValue("z", out _));
    }
}
