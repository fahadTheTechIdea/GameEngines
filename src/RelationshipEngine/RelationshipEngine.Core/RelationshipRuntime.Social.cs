using EngineCommon;
using EngineCommon.Graph;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    /// <summary>The standing of an actor nobody has ranked yet, and of every actor when standing is off: the village average (parts per thousand).</summary>
    public const long NeutralStanding = 1000;

    /// <summary>The most a standing can be: all the trust of the village on one actor, of the most actors it can hold (100,000 x 1000).</summary>
    public const long StandingMax = 100_000_000;

    /// <summary>The most that may separate two trust vectors for the iteration to stop early: one millionth of the whole.</summary>
    private const long StandingTolerance = TrustIteration.Scale / 1_000_000;

    private readonly GraphScratch _graphScratch = new();
    private readonly CommunitiesResult _communityResult = new();
    private readonly TrustResult _trustResult = new();
    private Dictionary<string, int>? _communitySizes;

    /// <summary>How many actors share the community of <paramref name="actor"/> (itself included).</summary>
    public int CommunitySize(ActorState actor)
    {
        if (_communitySizes is null)
        {
            var sizes = new Dictionary<string, int>(_actors.Count, StringComparer.Ordinal);
            foreach (var member in _actors.Values) sizes[member.Community] = sizes.GetValueOrDefault(member.Community) + 1;
            _communitySizes = sizes;
        }
        return _communitySizes.GetValueOrDefault(actor.Community, 1);
    }

    /// <summary>The actors in ordinal id order (the graph's node numbering) and the number each has.</summary>
    private (ActorState[] Nodes, Dictionary<string, int> Index) Numbered()
    {
        var nodes = _actors.Values.ToArray();
        var index = new Dictionary<string, int>(nodes.Length, StringComparer.Ordinal);
        for (var i = 0; i < nodes.Length; i++) index[nodes[i].Id] = i;
        return (nodes, index);
    }

    /// <summary>
    /// Communities: deterministic label propagation over the edges that like, from the opinions as they stand now (an actor's previous community is its starting label, so a community keeps its identity from day to day).
    /// Nothing here changes an opinion. Returns the graph it built (null when the pass is off or there is nobody), so the same step's standing pass can reuse it when it reads the same opinions. What moved is reported in <paramref name="changes"/>.
    /// </summary>
    private CsrGraph? LiveCommunities(RelationshipChanges changes)
    {
        var communities = Set.Social.Communities;
        if (_actors.Count == 0 || !communities.Enabled) return null;
        var (nodes, index) = Numbered();
        var graph = OpinionGraph(nodes, index, communities.Axis, communities.Above);
        FindCommunities(nodes, index, graph, communities, changes);
        _communitySizes = null;
        return graph;
    }

    /// <summary>
    /// Standing: EigenTrust-style trust iteration over the opinions as they stand now, with the document's pre-trust. <paramref name="communityGraph"/> is the graph the communities pass of the same step built; it is used
    /// only when that pass read the same axis and line, otherwise (and when null) the graph is built here.
    /// </summary>
    private void LiveStanding(RelationshipChanges changes, CsrGraph? communityGraph)
    {
        var standing = Set.Social.Standing;
        if (_actors.Count == 0 || !standing.Enabled) return;
        var communities = Set.Social.Communities;
        var (nodes, index) = Numbered();
        var shared = communityGraph is not null && communities.Axis == standing.Axis && communities.Above == standing.Above;
        var trust = shared ? communityGraph! : OpinionGraph(nodes, index, standing.Axis, standing.Above);
        var scores = Trust(nodes, trust, standing, _graphScratch, _trustResult);
        RecordStanding(nodes, scores, standing, changes);
    }

    /// <summary>The graph of who thinks well of whom: an edge for each opinion above the line, weighted by how far above it.</summary>
    private CsrGraph OpinionGraph(ActorState[] nodes, Dictionary<string, int> index, int axis, long above)
    {
        var code = GraphBuilder.Create(nodes.Length, out var builder);
        if (code.Length != 0) throw GraphRefused(code);
        for (var from = 0; from < nodes.Length; from++)
            foreach (var edge in nodes[from].Edges.Values)
            {
                var weight = OpinionOf(edge, axis) - above;
                if (weight <= 0 || !index.TryGetValue(edge.To, out var to)) continue;
                code = builder!.Add(from, to, weight);
                if (code.Length != 0) throw GraphRefused(code);
            }
        var built = builder!.Build();
        return built.Ok ? built.Graph! : throw GraphRefused(built.Code);
    }

    // The loader bounds the graph (nodes, edges, weights) inside the library's limits, so a refusal from it is a defect of this engine, not of a document: it is raised, never turned into an empty answer.
    private static EngineRefusedException GraphRefused(string code) => new(RelationshipCodes.Capacity, "the social graph was refused by the graph library: " + code);

    private void FindCommunities(ActorState[] nodes, Dictionary<string, int> index, CsrGraph graph, CommunityDef rules, RelationshipChanges changes)
    {
        var start = new int[nodes.Length];
        // An actor nobody it likes can anchor is alone: label propagation leaves a node with no liking edge on its starting label, so it must start on its own and not on the community it used to be in.
        for (var i = 0; i < nodes.Length; i++) start[i] = graph.OutDegree(i) == 0 ? i : index.GetValueOrDefault(nodes[i].Community, i);
        var found = LabelPropagation.Run(graph, start, rules.MaxRounds, _graphScratch, _communityResult);
        if (found.Code.Length != 0) throw GraphRefused(found.Code);
        for (var i = 0; i < nodes.Length; i++)
        {
            var name = nodes[found.Community[i]].Id;
            if (name == nodes[i].Community) continue;
            changes.Communities.Add(new CommunityChanged(nodes[i].Id, nodes[i].Community, name));
            nodes[i].Community = name;
        }
    }

    /// <summary>The pre-trust vector: the actors with the document's trait share the whole equally (the lowest ids take the remainder); nobody with it, or no trait named, leaves it empty, which the library takes as everyone equally.</summary>
    private static long[] PreTrust(ActorState[] nodes, StandingDef rules)
    {
        var trusted = new List<int>();
        if (rules.PreTrustTrait.Length > 0)
            for (var i = 0; i < nodes.Length; i++)
                if (nodes[i].Traits.Contains(rules.PreTrustTrait)) trusted.Add(i);
        var preTrust = new long[trusted.Count == 0 ? 0 : nodes.Length];
        for (var k = 0; k < trusted.Count; k++) preTrust[trusted[k]] = TrustIteration.Scale / trusted.Count + (k < TrustIteration.Scale % trusted.Count ? 1 : 0);
        return preTrust;
    }

    private static TrustResult Trust(ActorState[] nodes, CsrGraph graph, StandingDef rules, GraphScratch scratch, TrustResult reuse)
    {
        var found = TrustIteration.Run(graph, PreTrust(nodes, rules), rules.AlphaPermille * (TrustIteration.Scale / 1000), rules.Iterations, StandingTolerance, scratch, reuse);
        return found.Code.Length == 0 ? found : throw GraphRefused(found.Code);
    }

    /// <summary>The standing a trust score is: parts per thousand of the village average, so 1000 is an actor with an average share of the trust.</summary>
    internal static long StandingOf(long score, int actors) => (long)((Int128)score * actors * 1000 / TrustIteration.Scale);

    private static void RecordStanding(ActorState[] nodes, TrustResult scores, StandingDef rules, RelationshipChanges changes)
    {
        for (var i = 0; i < nodes.Length; i++)
        {
            var value = StandingOf(scores.Scores[i], nodes.Length);
            if (Math.Abs(value - nodes[i].Standing) >= rules.EventDelta) changes.Standing.Add(new StandingChanged(nodes[i].Id, nodes[i].Standing, value));
            nodes[i].Standing = value;
        }
    }

    /// <summary>The trust graph and scores as they would be computed this moment, for an explanation: the day boundary's arithmetic on the opinions as they stand now.</summary>
    internal (ActorState[] Nodes, Dictionary<string, int> Index, CsrGraph Graph, TrustResult Result) StandingNow()
    {
        var rules = Set.Social.Standing;
        var (nodes, index) = Numbered();
        var graph = OpinionGraph(nodes, index, rules.Axis, rules.Above);
        return (nodes, index, graph, Trust(nodes, graph, rules, new GraphScratch(), new TrustResult()));
    }
}
