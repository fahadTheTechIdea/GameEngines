using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalNetworkDestination(string Id,string Purpose,string SourceRuleId,MetadataRegionalEndpoint Endpoint);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalNetworkRule(string Id,string ConnectionKind,IReadOnlyList<MetadataRegionalNetworkDestination> Destinations,
    WorldNetworkOptions Options,WorldPolygon? RoutingDomain=null);
public sealed record MetadataRegionalNetworkLink(string NetworkConnectionId,string RegionalConnectionId);
public sealed record MetadataRegionalResolvedDestination(string DestinationId,MetadataRegionalEndpoint Endpoint);
public sealed record MetadataRegionalGeneratedNetwork(string RuleId,WorldDestinationNetworkPlan Plan,IReadOnlyList<MetadataRegionalNetworkLink> Links,long Work,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalResolvedDestination>? ResolvedDestinations=null);
public sealed record MetadataRegionalNetworkPlan(int Version,IReadOnlyList<MetadataRegionalGeneratedNetwork> Networks,long Work);

public sealed class MetadataRegionalNetworkSelectorException(string ruleId,string destinationId,MetadataRegionalEndpoint endpoint,int count,long work)
    :ArgumentException("METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:"+endpoint.RegionId+":"+endpoint.Selector!.NodeKind+":"+count)
{
    public string RuleId {get;}=ruleId;
    public string DestinationId {get;}=destinationId;
    public string RegionId {get;}=endpoint.RegionId;
    public int Count {get;}=count;
    public long Work {get;}=work;
}

public sealed class MetadataRegionalNetworkInfeasibleException(string ruleId,WorldDestinationNetworkPlan plan,long work)
    :ArgumentException("METADATA_REGIONAL_NETWORK_UNRESOLVED:"+ruleId+":"+plan.Status)
{
    public string RuleId {get;}=ruleId;
    public WorldDestinationNetworkPlan Plan {get;}=plan;
    public long Work {get;}=work;
}

/// <summary>The same network owner binds regional source endpoints, then delegates
/// all candidate search, terrain costs and connectivity decisions to the shared solver.</summary>
public sealed partial class MetadataNetworkPlanner
{
    internal static IReadOnlyList<MetadataRegionalNetworkRule>? CanonicalRegionalRules(IReadOnlyList<MetadataRegionalNetworkRule>? rules,
        WorldSurfaceDefinition surface,IReadOnlyList<string?> regionIds,IReadOnlyList<string?> authoredKinds,CancellationToken token)
    {
        if(rules is null)return null;
        token.ThrowIfCancellationRequested();
        if(rules.Count is <1 or >4||rules.Any(r=>r is null)||regionIds.Any(id=>!WorldEnvironmentQuery.Id(id)))throw new ArgumentException("METADATA_REGIONAL_NETWORK_RULES_INVALID");
        var source=new WorldSurfaceQuery(surface with{Terrain=null},token);var ids=new HashSet<string>(StringComparer.Ordinal);
        var kinds=authoredKinds.ToHashSet(StringComparer.Ordinal);var result=new List<MetadataRegionalNetworkRule>();long maximumWork=0;
        foreach(var rule in rules.OrderBy(r=>r.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var o=rule.Options;
            if(!WorldEnvironmentQuery.Id(rule.Id)||!ids.Add(rule.Id)||!WorldEnvironmentQuery.Id(rule.ConnectionKind)||!kinds.Add(rule.ConnectionKind)||
                rule.Destinations is not {Count:>=2 and <=32}||rule.Destinations.Any(d=>d is null)||o is null||!WorldEnvironmentQuery.Text(o.Purpose)||
                !WorldEnvironmentQuery.Text(o.SourceRuleId)||string.IsNullOrWhiteSpace(o.Scale)||o.Scale.Length>128||!Enum.IsDefined(o.Connectivity)||
                o.NearestNeighbors is <1 or >31||o.MaximumPairChecks is <1 or >496||o.MaximumWork is <1 or >200000000||o.Search is null||
                (o.RequiredConnections?.Count>496||o.ForbiddenConnections?.Count>496))throw new ArgumentException("METADATA_REGIONAL_NETWORK_RULE_INVALID");
            maximumWork+=o.MaximumWork;if(maximumWork>200000000)throw new ArgumentException("METADATA_REGIONAL_NETWORK_TOTAL_BUDGET");
            WorldSurfaceRoutePlanner.ValidateRequest(new(rule.Id,o.Purpose,o.SourceRuleId,new(0,0),new(1,1),o.Profile,o.Search));
            WorldSurfaceRoutePlanner.ValidateReuse(o.Reuse);
            WorldDestinationNetworkPlanner.ValidateRoundingReuse(o);
            var destinationIds=new HashSet<string>(StringComparer.Ordinal);var bindings=new HashSet<MetadataRegionalEndpoint>();
            foreach(var d in rule.Destinations)
                if(!WorldEnvironmentQuery.Id(d.Id)||!destinationIds.Add(d.Id)||!WorldEnvironmentQuery.Text(d.Purpose)||!WorldEnvironmentQuery.Text(d.SourceRuleId)||
                    d.Endpoint is null||!regionIds.Contains(d.Endpoint.RegionId,StringComparer.Ordinal)||
                    (d.Endpoint.Selector is null?!WorldEnvironmentQuery.Text(d.Endpoint.NodeId)||d.Endpoint.NodeId.Length>2048:
                        d.Endpoint.NodeId!=""||!WorldEnvironmentQuery.Id(d.Endpoint.Selector.NodeKind)||d.Endpoint.Selector.ArchetypeId is {} archetype&&!WorldEnvironmentQuery.Id(archetype))||
                    d.Endpoint.PortId is not null&&!WorldEnvironmentQuery.Id(d.Endpoint.PortId)||!bindings.Add(d.Endpoint))throw new ArgumentException("METADATA_REGIONAL_NETWORK_DESTINATION_INVALID");
            var domain=rule.RoutingDomain??surface.Boundary;
            if(domain?.Vertices is not {Count:>=3 and <=64})throw new ArgumentException("METADATA_REGIONAL_NETWORK_DOMAIN_INVALID");
            domain.Validate();if(surface.Boundary is null||!surface.Boundary.ContainsPolygon(domain))throw new ArgumentException("METADATA_REGIONAL_NETWORK_DOMAIN_OUTSIDE");
            var profile=new WorldSurfaceTraversal(source.RestrictTo(domain,token),o.Profile,token).Profile;
            var pairs=new HashSet<(string,string)>();
            foreach(var pair in o.RequiredConnections??[])
            {
                if(pair is null||!destinationIds.Contains(pair.FromId)||!destinationIds.Contains(pair.ToId)||pair.FromId==pair.ToId)
                    throw new ArgumentException("METADATA_REGIONAL_NETWORK_REQUIRED_INVALID");
                var canonical=StringComparer.Ordinal.Compare(pair.FromId,pair.ToId)<0?(pair.FromId,pair.ToId):(pair.ToId,pair.FromId);
                if(!pairs.Add(canonical))throw new ArgumentException("METADATA_REGIONAL_NETWORK_REQUIRED_INVALID");
            }
            var forbidden=new HashSet<(string,string)>();
            foreach(var pair in o.ForbiddenConnections??[])
            {
                if(pair is null||!destinationIds.Contains(pair.FromId)||!destinationIds.Contains(pair.ToId)||pair.FromId==pair.ToId)
                    throw new ArgumentException("METADATA_REGIONAL_NETWORK_FORBIDDEN_INVALID");
                var canonical=StringComparer.Ordinal.Compare(pair.FromId,pair.ToId)<0?(pair.FromId,pair.ToId):(pair.ToId,pair.FromId);
                if(!forbidden.Add(canonical))throw new ArgumentException("METADATA_REGIONAL_NETWORK_FORBIDDEN_INVALID");
            }
            if(pairs.Overlaps(forbidden))throw new ArgumentException("METADATA_REGIONAL_NETWORK_REQUIRED_FORBIDDEN_CONFLICT");
            // Test the most permissive graph. Terrain can remove edges, never add a forbidden edge.
            var ordered=destinationIds.Order(StringComparer.Ordinal).ToArray();
            var possible=new List<WorldNetworkLink>();
            for(int a=0;a<ordered.Length;a++)for(int b=a+1;b<ordered.Length;b++)
                if(!forbidden.Contains((ordered[a],ordered[b])))possible.Add(new WorldNetworkLink($"{a}:{b}",ordered[a],ordered[b]));
            var structural=new WorldNetworkConnectivityAnalyzer().Analyze(ordered,possible,token);
            if(structural.Components.Count!=1)throw new ArgumentException("METADATA_REGIONAL_NETWORK_ALLOWED_GRAPH_DISCONNECTED:"+rule.Id);
            if(o.Connectivity==WorldNetworkConnectivity.SingleConnectionFailure&&structural.Bridges.Count>0)
                throw new ArgumentException("METADATA_REGIONAL_NETWORK_ALLOWED_GRAPH_NOT_REDUNDANT:"+rule.Id);
            result.Add(rule with{Destinations=rule.Destinations.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray(),Options=o with{Profile=profile,
                RequiredConnections=pairs.OrderBy(p=>p.Item1,StringComparer.Ordinal).ThenBy(p=>p.Item2,StringComparer.Ordinal).Select(p=>new WorldRequiredConnection(p.Item1,p.Item2)).ToArray(),
                ForbiddenConnections=o.ForbiddenConnections is null?null:forbidden.OrderBy(p=>p.Item1,StringComparer.Ordinal).ThenBy(p=>p.Item2,StringComparer.Ordinal).Select(p=>new WorldRequiredConnection(p.Item1,p.Item2)).ToArray()}});
        }
        return result.ToArray();
    }

    internal (MetadataRegionalNetworkPlan? Plan,IReadOnlyList<MetadataRegionalRoute> Routes) PlanRegional(MetadataRegionalWorldDefinition definition,
        IReadOnlyList<WorldSurfaceExclusion> obstacles,Func<MetadataRegionalEndpoint,WorldPolygon,WorldTraversalProfile,WorldPoint> endpoint,
        Func<string,string,MetadataRegionalEndpoint,MetadataRegionalEndpoint> resolve,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.NetworkRules is null)return(null,[]);
        long before=budget.Used;var networks=new List<MetadataRegionalGeneratedNetwork>();var routes=new List<MetadataRegionalRoute>();
        var ids=definition.Connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var rule in definition.NetworkRules)
        {
            token.ThrowIfCancellationRequested();long start=budget.Used;budget.Charge(1L+rule.Destinations.Count);
            var domain=rule.RoutingDomain??definition.Surface.Boundary!;
            var bound=rule.Destinations.Select(d=>d with{Endpoint=resolve(rule.Id,d.Id,d.Endpoint)}).ToDictionary(d=>d.Id,StringComparer.Ordinal);
            var destinations=rule.Destinations.Select(d=>new WorldNetworkDestination(d.Id,d.Purpose,endpoint(bound[d.Id].Endpoint,domain,rule.Options.Profile))).ToArray();
            var query=WorldSurfaceQuery.CreateRegional(definition.Surface,domain,token,obstacles);
            var plan=new WorldDestinationNetworkPlanner().Plan(query,new(definition.Id+"/regional-network/"+rule.Id,destinations,rule.Options),token,budget);
            if(plan.Status!=WorldNetworkStatus.Found)throw new MetadataRegionalNetworkInfeasibleException(rule.Id,plan,budget.Used);
            var links=new List<MetadataRegionalNetworkLink>();
            foreach(var edge in plan.Edges)
            {
                budget.Charge();string id="network-"+MetadataExecutionCache.Hash(edge.Link.Id)[..32];
                if(!ids.Add(id))throw new ArgumentException("METADATA_REGIONAL_NETWORK_ID_CONFLICT");
                var from=bound[edge.Link.FromId].Endpoint;var to=bound[edge.Link.ToId].Endpoint;
                var connection=new MetadataRegionalConnection(id,rule.ConnectionKind,plan.Options.Purpose,plan.Options.SourceRuleId,from,to,from.RegionId,domain,
                    plan.Options.Profile,rule.Options.Search,true);
                routes.Add(new(connection,plan.Destinations.Single(d=>d.Id==edge.Link.FromId).Position,plan.Destinations.Single(d=>d.Id==edge.Link.ToId).Position,
                    edge.Forward with{Id=id+"/forward"},edge.Reverse with{Id=id+"/reverse"}));
                links.Add(new(edge.Link.Id,id));
                if(definition.Connections.Count+routes.Count>128)throw new ArgumentException("METADATA_REGIONAL_NETWORK_CONNECTION_BUDGET");
            }
            var resolved=rule.Destinations.Any(d=>d.Endpoint.Selector is not null)?rule.Destinations.Select(d=>new MetadataRegionalResolvedDestination(d.Id,bound[d.Id].Endpoint)).ToArray():null;
            networks.Add(new(rule.Id,plan,links.ToArray(),budget.Used-start,resolved));
        }
        return(new(1,networks.ToArray(),budget.Used-before),routes.OrderBy(r=>r.Definition.Id,StringComparer.Ordinal).ToArray());
    }
}
