namespace EngineCommon.Graph;

/// <summary>One edge's share of a node's trust: <see cref="From"/> trusts the node, and <see cref="Amount"/> (parts of <see cref="TrustIteration.Scale"/>) of the node's trust comes through that edge, before mixing in the pre-trust.</summary>
public readonly record struct TrustContribution(int From, int Edge, long Amount);

/// <summary>
/// The trust scores found: one per node, in parts of <see cref="TrustIteration.Scale"/>, summing to exactly Scale. <see cref="Residual"/> is the L1 distance between the last two vectors (parts of Scale).
/// <see cref="LocalTrust"/> is each edge's normalised row share (a row of positive edges sums to exactly Scale).
/// </summary>
public sealed class TrustResult
{
    internal long[] scores = [];
    internal long[] localTrust = [];

    public string Code { get; internal set; } = GraphCodes.Ok;
    public int NodeCount { get; internal set; }
    public int EdgeCount { get; internal set; }
    public int Iterations { get; internal set; }
    public long Residual { get; internal set; }
    public bool Converged { get; internal set; }
    public ReadOnlySpan<long> Scores => scores.AsSpan(0, NodeCount);
    public ReadOnlySpan<long> LocalTrust => localTrust.AsSpan(0, EdgeCount);

    /// <summary>The <paramref name="top"/> largest direct contributions to a node's trust, largest first, the lowest edge id first among equals. O(E).</summary>
    public List<TrustContribution> Contributors(CsrGraph graph, int node, int top)
    {
        var all = new List<TrustContribution>();
        for (var e = 0; e < graph.EdgeCount; e++)
        {
            if (graph.Target(e) != node || localTrust[e] == 0) continue;
            all.Add(new TrustContribution(graph.Source(e), e, (long)((Int128)localTrust[e] * scores[graph.Source(e)] / TrustIteration.Scale)));
        }
        all.Sort((a, b) => a.Amount != b.Amount ? b.Amount.CompareTo(a.Amount) : a.Edge.CompareTo(b.Edge));
        if (all.Count > top) all.RemoveRange(top, all.Count - top);
        return all;
    }

    public string Explain() => Code.Length != 0
        ? "refused " + Code
        : (Converged ? "converged" : "stopped") + " after " + Iterations + " iterations, residual " + Residual + " of " + TrustIteration.Scale;
}

/// <summary>
/// Fixed-point EigenTrust-style power iteration: t' = (1 - a) C^T t + a p, where C is the edge weights with negatives clipped to 0 and each row normalised to sum to Scale, a node with no positive edge trusts according to
/// the pre-trust vector p (its row is p), and a is the pre-trust weight. All values are integers in parts of <see cref="Scale"/> (10^9); products are 128-bit.
/// Rounding, stated: row shares and each new vector are floors of exact shares, and the units the floors lose are given one each to the entries with the largest remainder, ties to the lowest index, so every row of C and
/// every vector sums to exactly Scale. Convergence, stated: the exact map contracts the L1 distance by (Scale - a) / Scale each step (C^T preserves the L1 norm of a distribution), so for a &gt; 0 the residual falls
/// geometrically until it meets the rounding floor, at most about 2 x V x Scale / a parts; with a = 0 there is no such guarantee (it needs a strongly connected, aperiodic graph) and the run simply stops at the limit.
/// Starting vector is p. O(iterations x E) plus a sort per iteration to hand out the rounding units.
/// </summary>
public static class TrustIteration
{
    public const long Scale = 1_000_000_000;

    /// <summary>Runs with the pre-trust vector (empty: uniform) and weight <paramref name="alpha"/> in 0 to Scale, until the residual is at most <paramref name="epsilon"/> or <paramref name="maxIterations"/> have run.</summary>
    public static TrustResult Run(CsrGraph graph, ReadOnlySpan<long> preTrust, long alpha, int maxIterations, long epsilon, GraphScratch scratch, TrustResult? reuse = null)
    {
        var result = reuse ?? new TrustResult();
        var n = graph.NodeCount;
        var m = graph.EdgeCount;
        result.Code = GraphCodes.Ok;
        result.NodeCount = n;
        result.EdgeCount = m;
        result.Iterations = 0;
        result.Residual = 0;
        result.Converged = false;
        if (scratch is null) { result.Code = GraphCodes.ArgumentMissing; return result; }
        if (preTrust.Length != 0 && preTrust.Length != n) { result.Code = GraphCodes.PreTrustLength; return result; }
        long sum = 0;
        foreach (var p in preTrust)
        {
            if (p < 0) { result.Code = GraphCodes.PreTrustNegative; return result; }
            sum += p;
        }
        if (preTrust.Length != 0 && sum != Scale) { result.Code = GraphCodes.PreTrustSum; return result; }
        if (alpha < 0 || alpha > Scale) { result.Code = GraphCodes.AlphaRange; return result; }
        if (maxIterations < 1 || maxIterations > GraphLimits.MaxIterations) { result.Code = GraphCodes.IterationsRange; return result; }
        if (epsilon < 0) { result.Code = GraphCodes.EpsilonRange; return result; }
        if (result.scores.Length < n) result.scores = new long[n];
        if (result.localTrust.Length < m) result.localTrust = new long[m];
        if (n == 0) { result.Converged = true; return result; }

        var pre = scratch.Longs(0, n);
        if (preTrust.Length == 0)
        {
            for (var i = 0; i < n; i++) pre[i] = Scale / n + (i < Scale % n ? 1 : 0);
        }
        else
        {
            preTrust.CopyTo(pre);
        }

        var dangling = scratch.Ints(0, n);
        NormaliseRows(graph, result.localTrust, dangling, scratch);

        var current = result.scores;
        var next = scratch.Longs(1, n);
        var remainders = scratch.Longs(2, n);
        var order = scratch.Ints(1, n);
        var wide = scratch.Wides(0, n);
        for (var i = 0; i < n; i++) current[i] = pre[i];
        var keep = Scale - alpha;
        var unit = (Int128)Scale * Scale;
        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            Array.Clear(wide, 0, n);
            long danglingMass = 0;
            for (var i = 0; i < n; i++)
            {
                if (dangling[i] != 0) { danglingMass += current[i]; continue; }
                for (var e = graph.FirstEdge(i); e < graph.EndEdge(i); e++)
                    wide[graph.Target(e)] += (Int128)(keep * result.localTrust[e]) * current[i];
            }
            long total = 0;
            for (var j = 0; j < n; j++)
            {
                var exact = wide[j] + (Int128)keep * pre[j] * danglingMass + (Int128)alpha * Scale * pre[j];
                next[j] = (long)(exact / unit);
                remainders[j] = (long)(exact % unit);
                total += next[j];
            }
            scratch.Order.Distribute(order, remainders, next, n, Scale - total);
            long residual = 0;
            for (var j = 0; j < n; j++)
            {
                residual += Math.Abs(next[j] - current[j]);
                current[j] = next[j];
            }
            result.Iterations = iteration;
            result.Residual = residual;
            if (residual <= epsilon) { result.Converged = true; break; }
        }
        return result;
    }

    /// <summary>Each row of positive weights becomes shares summing to exactly Scale (floors, then the lost units to the largest remainders); a row with no positive weight is marked dangling.</summary>
    private static void NormaliseRows(CsrGraph graph, long[] shares, int[] dangling, GraphScratch scratch)
    {
        var widest = 0;
        for (var i = 0; i < graph.NodeCount; i++) widest = Math.Max(widest, graph.OutDegree(i));
        var rowShares = scratch.Longs(3, widest);
        var rowRemainders = scratch.Longs(4, widest);
        var order = scratch.Ints(2, widest);
        for (var i = 0; i < graph.NodeCount; i++)
        {
            var first = graph.FirstEdge(i);
            var degree = graph.OutDegree(i);
            long rowSum = 0;
            for (var k = 0; k < degree; k++) rowSum += Math.Max(graph.Weight(first + k), 0);
            dangling[i] = rowSum == 0 ? 1 : 0;
            if (rowSum == 0)
            {
                for (var k = 0; k < degree; k++) shares[first + k] = 0;
                continue;
            }
            long given = 0;
            for (var k = 0; k < degree; k++)
            {
                var numerator = Math.Max(graph.Weight(first + k), 0) * Scale;
                rowShares[k] = numerator / rowSum;
                rowRemainders[k] = numerator % rowSum;
                given += rowShares[k];
            }
            scratch.Order.Distribute(order, rowRemainders, rowShares, degree, Scale - given);
            for (var k = 0; k < degree; k++) shares[first + k] = rowShares[k];
        }
    }
}
