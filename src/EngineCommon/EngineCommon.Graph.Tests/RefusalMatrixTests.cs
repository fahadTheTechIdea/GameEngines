using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

/// <summary>Every code the library declares is reached by a call that returns it, and no call returns a code that is not declared.</summary>
public sealed class RefusalMatrixTests
{
    [Fact]
    public void EveryDeclaredCodeIsReachedByARealRefusal()
    {
        var g = Make(3, W(0, 1, 1), W(1, 2, 1));
        var s = new GraphScratch();
        GraphBuilder.Create(2, out var b);
        var reached = new List<string>
        {
            GraphBuilder.Create(-1, out _),
            b!.Add(0, 5, 1),
            b.Add(0, 1, GraphLimits.MaxWeight + 1),
            b.Add(0, 1, 1, -1),
            g.Reweight([1]),
            Dijkstra.Run(g, 0, -1, null!).Code,
            Dijkstra.Run(Make(2, W(0, 1, -1)), 0, -1, s).Code,
            Dijkstra.Run(g, 7, -1, s).Code,
            Dijkstra.Run(g, 0, 7, s).Code,
            MinCostFlow.Run(g, 0, 0, 1, 1, s).Code,
            MinCostFlow.Run(g, 0, 2, -1, 1, s).Code,
            MinCostFlow.Run(g, 0, 2, 1, 0, s).Code,
            Bfs.Run(g, [0], -1).Code,
            LabelPropagation.Run(g, [], 0, s).Code,
            LabelPropagation.Run(g, [0], 1, s).Code,
            LabelPropagation.Run(g, [0, 1, 9], 1, s).Code,
            TrustIteration.Run(g, [S], 1, 1, 0, s).Code,
            TrustIteration.Run(g, [-1, 1, S], 1, 1, 0, s).Code,
            TrustIteration.Run(g, [1, 1, 1], 1, 1, 0, s).Code,
            TrustIteration.Run(g, [], -1, 1, 0, s).Code,
            TrustIteration.Run(g, [], 1, 0, 0, s).Code,
            TrustIteration.Run(g, [], 1, 1, -1, s).Code,
            DegreeTable.Run(g).Top(DegreeMeasure.OutDegree, 0).Code,
            BellmanFord.Run(BigWeightPath(), 0, s).Code,
            TopologicalSort.Run(Make(2, W(0, 1, 1), W(1, 0, 1)), s).Code,
            TopologicalSort.Run(g, null!).Code,
        };
        for (var i = 0; i <= GraphLimits.MaxEdges; i++) { var code = b.Add(0, 1, 1); if (code.Length != 0) { reached.Add(code); break; } }
        reached.Add(GraphBuilder.Create(GraphLimits.MaxNodes + 1, out _));
        reached.Add(MinCostFlow.Run(g, 9, 2, 1, 1, s).Code);
        reached.Add(MinCostFlow.Run(g, 0, 9, 1, 1, s).Code);

        var declared = typeof(GraphCodes).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).Where(c => c.Length != 0).ToHashSet();
        Assert.Equal(declared.OrderBy(c => c, StringComparer.Ordinal), reached.Where(c => c.Length != 0).Distinct().OrderBy(c => c, StringComparer.Ordinal));
        Assert.All(declared, c => Assert.StartsWith("GRAPH_", c));
        Assert.Equal(declared.Count, declared.Distinct().Count());
        Assert.Equal("", GraphCodes.Ok);
    }

    private const long S = TrustIteration.Scale;

    private static CsrGraph BigWeightPath()
    {
        GraphBuilder.Create(GraphLimits.MaxNodes, out var b);
        for (var i = 0; i < 50_000; i++) b!.Add(i, i + 1, GraphLimits.MaxWeight);
        return b!.Build().Graph!;
    }
}
