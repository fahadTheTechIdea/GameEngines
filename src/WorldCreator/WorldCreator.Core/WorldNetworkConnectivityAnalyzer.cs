namespace WorldCreator.Core.Spatial;

/// <summary>Connected components and single-edge cuts in a simple undirected destination graph.
/// It makes no inference about physical route intersections or shared terrain corridors.</summary>
public sealed class WorldNetworkConnectivityAnalyzer
{
    public WorldNetworkAnalysis Analyze(IReadOnlyList<string> destinations,IReadOnlyList<WorldNetworkLink> links,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(destinations is null || destinations.Count is <1 or >32 || links is null || links.Count>496 ||
            destinations.Any(string.IsNullOrWhiteSpace) || destinations.Distinct(StringComparer.Ordinal).Count()!=destinations.Count)
            throw new ArgumentException("WORLD_NETWORK_GRAPH_INVALID");
        var ids=destinations.Order(StringComparer.Ordinal).ToArray();
        var neighbors=ids.ToDictionary(id=>id,_=>new List<(string Node,string Edge)>(),StringComparer.Ordinal);
        var edgeIds=new HashSet<string>(StringComparer.Ordinal);var pairs=new HashSet<(string,string)>();
        foreach(var link in links)
        {
            token.ThrowIfCancellationRequested();
            if(link is null || string.IsNullOrWhiteSpace(link.Id) || !edgeIds.Add(link.Id) ||
                !neighbors.ContainsKey(link.FromId)||!neighbors.ContainsKey(link.ToId)||link.FromId==link.ToId)
                throw new ArgumentException("WORLD_NETWORK_GRAPH_EDGE_INVALID");
            var pair=string.CompareOrdinal(link.FromId,link.ToId)<0?(link.FromId,link.ToId):(link.ToId,link.FromId);
            if(!pairs.Add(pair))throw new ArgumentException("WORLD_NETWORK_GRAPH_PARALLEL_EDGE");
            neighbors[link.FromId].Add((link.ToId,link.Id));neighbors[link.ToId].Add((link.FromId,link.Id));
        }
        var discovery=new Dictionary<string,int>(StringComparer.Ordinal);var low=new Dictionary<string,int>(StringComparer.Ordinal);
        var components=new List<IReadOnlyList<string>>();var bridges=new List<string>();int time=0;
        void Visit(string node,string? parentEdge,List<string> members)
        {
            token.ThrowIfCancellationRequested();discovery[node]=low[node]=++time;members.Add(node);
            foreach(var (next,edge) in neighbors[node].OrderBy(n=>n.Node,StringComparer.Ordinal))
            {
                if(edge==parentEdge)continue;
                if(!discovery.ContainsKey(next))
                {
                    Visit(next,edge,members);low[node]=Math.Min(low[node],low[next]);
                    if(low[next]>discovery[node])bridges.Add(edge);
                }
                else low[node]=Math.Min(low[node],discovery[next]);
            }
        }
        foreach(var id in ids)if(!discovery.ContainsKey(id))
        {
            var members=new List<string>();Visit(id,null,members);
            components.Add(Array.AsReadOnly(members.Order(StringComparer.Ordinal).ToArray()));
        }
        return new(components.AsReadOnly(),Array.AsReadOnly(bridges.Order(StringComparer.Ordinal).ToArray()));
    }
}
