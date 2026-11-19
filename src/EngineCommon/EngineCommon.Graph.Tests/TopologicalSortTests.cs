using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

/// <summary>
/// The topological order and its cycle report. Oracles are independent of the library: a transitive closure (Warshall) decides whether a graph has a cycle at all, and a quadratic simulation that scans for the lowest
/// available node each step gives the order the stated tie-break must produce.
/// </summary>
public sealed class TopologicalSortTests
{
    [Fact]
    public void AHandWorkedGraphGivesTheLowestIndexFirstOrder()
    {
        // 3 -> 1, 3 -> 0, 1 -> 2, 0 -> 2, 4 alone. Worked on paper: the available set starts as {3, 4} and 3 goes (lowest of the two); that frees 0 and 1, so the set is {0, 1, 4} and 0 goes;
        // then {1, 4} and 1 goes, which frees 2 (its other predecessor 0 is placed), so the set is {2, 4}; 2 goes, then 4.
        var graph = Make(5, W(3, 1, 1), W(3, 0, 1), W(1, 2, 1), W(0, 2, 1));
        var result = TopologicalSort.Run(graph, new GraphScratch());
        Assert.Equal("", result.Code);
        Assert.Equal([3, 0, 1, 2, 4], result.Order.ToArray());
        Assert.Equal("ordered 5 nodes", result.Explain());
        Assert.Empty(result.Cycle);
    }

    [Fact]
    public void AnEmptyGraphAndANodeWithNoEdgesAreOrdered()
    {
        Assert.Empty(TopologicalSort.Run(Make(0), new GraphScratch()).Order.ToArray());
        Assert.Equal([0, 1, 2], TopologicalSort.Run(Make(3), new GraphScratch()).Order.ToArray());
    }

    [Fact]
    public void ASelfLoopAndATwoNodeCycleAreCyclesAndNamed()
    {
        var self = TopologicalSort.Run(Make(3, W(0, 1, 1), W(1, 1, 1)), new GraphScratch());
        Assert.Equal(GraphCodes.Cycle, self.Code);
        Assert.Equal([1], self.Cycle);
        Assert.Equal([0, 2], self.Order.ToArray()); // node 2 has no edges and is ordered; node 1 waits on itself
        Assert.Equal("cycle of 1 nodes: 1 > 1", self.Explain());
        var two = TopologicalSort.Run(Make(2, W(1, 0, 1), W(0, 1, 1)), new GraphScratch());
        Assert.Equal([0, 1], two.Cycle);
        Assert.Equal("cycle of 2 nodes: 0 > 1 > 0", two.Explain());
    }

    [Fact]
    public void ACycleDownstreamOfAnAcyclicPartIsFoundNotItsTail()
    {
        // 0 -> 1 -> 2 -> 3 -> 1 and 3 -> 4: nodes 1, 2, 3 form the cycle; 0 is ordered; 4 hangs off the cycle.
        var result = TopologicalSort.Run(Make(5, W(0, 1, 1), W(1, 2, 1), W(2, 3, 1), W(3, 1, 1), W(3, 4, 1)), new GraphScratch());
        Assert.Equal(GraphCodes.Cycle, result.Code);
        Assert.Equal([1, 2, 3], result.Cycle);
        Assert.Equal([0], result.Order.ToArray());
    }

    [Fact]
    public void ANullScratchIsRefusedByCode() => Assert.Equal(GraphCodes.ArgumentMissing, TopologicalSort.Run(Make(2, W(0, 1, 1)), null!).Code);

    [Fact]
    public void RandomGraphsAgreeWithTheClosureOracleTheQuadraticOrderAndTheirCycleIsReal()
    {
        var rng = new Rng(20261046);
        var scratch = new GraphScratch();
        var reused = new TopologicalOrderResult();
        var acyclic = 0;
        var cyclic = 0;
        for (var round = 0; round < 600; round++)
        {
            var nodes = 1 + rng.Below(12);
            var count = rng.Below(nodes * 2 + 1);
            var edges = new List<E>();
            for (var i = 0; i < count; i++)
            {
                var a = rng.Below(nodes);
                var b = rng.Below(nodes);
                // Odd rounds draw edges forward only (low to high), so acyclic graphs are common; even rounds allow any edge.
                if (round % 2 == 1)
                {
                    if (a == b) continue;
                    (a, b) = (Math.Min(a, b), Math.Max(a, b));
                }
                edges.Add(W(a, b, 1));
            }
            var graph = Make(nodes, Shuffled(rng, edges));
            var result = TopologicalSort.Run(graph, scratch, reused);
            Assert.Same(reused, result);
            var reach = Closure(nodes, edges);
            var hasCycle = Enumerable.Range(0, nodes).Any(v => reach[v, v]);
            Assert.Equal(hasCycle, result.Code == GraphCodes.Cycle);
            if (!hasCycle)
            {
                acyclic++;
                Assert.Equal(QuadraticOrder(nodes, edges), result.Order.ToArray());
                var position = new int[nodes];
                for (var i = 0; i < nodes; i++) position[result.Order[i]] = i;
                foreach (var e in edges) Assert.True(position[e.From] < position[e.To]);
            }
            else
            {
                cyclic++;
                var cycle = result.Cycle;
                Assert.NotEmpty(cycle);
                Assert.Equal(cycle.Min(), cycle[0]);
                for (var i = 0; i < cycle.Length; i++) Assert.Contains(edges, e => e.From == cycle[i] && e.To == cycle[(i + 1) % cycle.Length]);
                Assert.Equal(cycle.Length, cycle.Distinct().Count());
                // Every node ordered before the sort stuck lies on no cycle and is not downstream of one.
                foreach (var v in result.Order.ToArray()) Assert.Equal(0, Enumerable.Range(0, nodes).Count(u => reach[u, u] && reach[u, v]));
            }
            // The same edges added in another order give the same answer.
            var again = TopologicalSort.Run(Make(nodes, Shuffled(rng, edges)), scratch);
            Assert.Equal(result.Order.ToArray(), again.Order.ToArray());
            Assert.Equal(result.Cycle, again.Cycle);
        }
        Assert.True(acyclic > 150 && cyclic > 100, $"the generator must make both kinds: {acyclic} acyclic, {cyclic} cyclic");
    }

    [Fact]
    public void ReusingAResultAndScratchAllocatesNothingAfterWarmUp()
    {
        var graph = Make(200, Enumerable.Range(0, 199).Select(i => W(i, i + 1, 1)).ToArray());
        var scratch = new GraphScratch();
        var result = new TopologicalOrderResult();
        TopologicalSort.Run(graph, scratch, result);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 50; i++) TopologicalSort.Run(graph, scratch, result);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static bool[,] Closure(int nodes, List<E> edges)
    {
        var reach = new bool[nodes, nodes];
        foreach (var e in edges) reach[e.From, e.To] = true;
        for (var k = 0; k < nodes; k++)
            for (var i = 0; i < nodes; i++)
                for (var j = 0; j < nodes; j++)
                    if (reach[i, k] && reach[k, j]) reach[i, j] = true;
        return reach;
    }

    private static int[] QuadraticOrder(int nodes, List<E> edges)
    {
        var placed = new bool[nodes];
        var order = new List<int>();
        while (order.Count < nodes)
        {
            var next = Enumerable.Range(0, nodes).First(v => !placed[v] && edges.All(e => e.To != v || placed[e.From]));
            placed[next] = true;
            order.Add(next);
        }
        return order.ToArray();
    }
}
