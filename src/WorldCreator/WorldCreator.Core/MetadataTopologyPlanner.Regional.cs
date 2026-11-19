using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalTopologyDestination(string Id,string Kind,string Purpose,string SourceRuleId,MetadataRegionalEndpoint Endpoint);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalTopologyRule(MetadataTopologyRule Topology,WorldTraversalProfile Profile,WorldRouteSearch Search,WorldPolygon? RoutingDomain=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalTopologyDefinition(int Version,string Id,string Purpose,string SourceRuleId,
    IReadOnlyList<MetadataRegionalTopologyDestination> Destinations,IReadOnlyList<MetadataRegionalTopologyRule> Rules);
public sealed record MetadataRegionalTopologyLink(string LogicalConnectionId,string RegionalConnectionId);
public sealed record MetadataRegionalTopologyPlan(int Version,MetadataRegionalTopologyDefinition Definition,
    IReadOnlyList<WorldMetadataNode> Nodes,MetadataTopologyPlan Topology,MetadataProgressionPlan Progression,
    IReadOnlyList<MetadataRegionalConnection> SourceConnections,IReadOnlyList<MetadataRegionalConnection> Connections,
    IReadOnlyList<MetadataRegionalTopologyLink> Links,long Work);

public sealed partial class MetadataTopologyPlanner
{
    public static string RegionalDestinationId(string worldId,string destinationId)
    {
        if(!WorldEnvironmentQuery.Id(worldId)||!WorldEnvironmentQuery.Id(destinationId))throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_ID_INVALID");
        return worldId+"/destinations/"+destinationId;
    }
    internal MetadataRegionalTopologyPlan PlanRegional(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();long before=budget.Used;var definition=source.Topology;
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Text(definition.Purpose)||!WorldEnvironmentQuery.Text(definition.SourceRuleId)||
            definition.Destinations is not {Count:>=2 and <=16}||definition.Rules is not {Count:>=1 and <=16}||definition.Destinations.Any(d=>d is null)||definition.Rules.Any(r=>r is null))
            throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_INVALID");
        definition=MetadataJsonStorage.Snapshot(definition);
        definition=definition with{Destinations=definition.Destinations.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray(),Rules=definition.Rules.OrderBy(r=>r.Topology?.Id,StringComparer.Ordinal).ToArray()};
        budget.Charge(1L+source.Regions.Count+source.Connections.Count+definition.Destinations.Count+definition.Rules.Count);
        var ids=new HashSet<string>(StringComparer.Ordinal);var owners=new HashSet<string>(StringComparer.Ordinal);var regionIds=source.Regions.Select(r=>r.Id).ToHashSet(StringComparer.Ordinal);
        var nodes=new List<WorldMetadataNode>{new(source.Id,"world-topology-scope","world-topology-scope",null,definition.Purpose,new Dictionary<string,JsonElement>(),null,null)};
        var destinations=new Dictionary<string,MetadataRegionalTopologyDestination>(StringComparer.Ordinal);
        foreach(var d in definition.Destinations)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(!WorldEnvironmentQuery.Id(d.Id)||!ids.Add(d.Id)||!WorldEnvironmentQuery.Id(d.Kind)||!WorldEnvironmentQuery.Text(d.Purpose)||!WorldEnvironmentQuery.Text(d.SourceRuleId)||
                d.Endpoint is null||d.Endpoint.Selector is not null||!regionIds.Contains(d.Endpoint.RegionId)||!owners.Add(d.Endpoint.RegionId)||!WorldEnvironmentQuery.Text(d.Endpoint.NodeId)||d.Endpoint.NodeId.Length>2048||
                d.Endpoint.PortId is not null&&!WorldEnvironmentQuery.Id(d.Endpoint.PortId))throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_DESTINATION_INVALID");
            string id=RegionalDestinationId(source.Id,d.Id);destinations.Add(id,d);
            nodes.Add(new(id,"regional-destination",d.Kind,source.Id,d.Purpose,new Dictionary<string,JsonElement>{["source_rule_id"]=JsonSerializer.SerializeToElement(d.SourceRuleId)},null,null));
        }
        if(source.Connections.Any(c=>c is null))throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_SOURCE_CONNECTION_INVALID");
        var rules=new Dictionary<string,MetadataRegionalTopologyRule>(StringComparer.Ordinal);var existingKinds=source.Connections.Select(c=>c.Kind).ToHashSet(StringComparer.Ordinal);
        foreach(var r in definition.Rules)
        {
            budget.Charge();
            if(r.Topology is null||!WorldEnvironmentQuery.Id(r.Topology.Id)||r.Profile is null||r.Search is null||!rules.TryAdd(r.Topology.Id,r)||r.Topology.ScopeNodeId!=source.Id||r.Topology.LayerId is not null||
                existingKinds.Contains(r.Topology.ConnectionKind))throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_RULE_INVALID");
            var domain=r.RoutingDomain??source.Surface.Boundary;
            if(domain?.Vertices is not {Count:>=3 and <=64})throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_ROUTE_DOMAIN_INVALID");
            domain.Validate();WorldSurfaceRoutePlanner.ValidateRequest(new(r.Topology.Id,r.Topology.Purpose,r.Topology.SourceRuleId,new(0,0),new(1,1),r.Profile,r.Search));
            _=new WorldSurfaceTraversal(new WorldSurfaceQuery(source.Surface with{Terrain=null},token),r.Profile,token);
        }
        var topology=Plan(source.Id,definition.Rules.Select(r=>r.Topology).ToArray(),nodes,[],budget,token);
        var selected=topology.Results.SelectMany(r=>r.Connections).SelectMany(c=>new[]{c.FromId,c.ToId}).ToHashSet(StringComparer.Ordinal);
        if(!selected.SetEquals(destinations.Keys))throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_DESTINATION_UNUSED");
        var progression=new MetadataProgressionPlanner().Plan(ProgressionRules(topology),nodes,topology.Results.SelectMany(r=>r.Connections).ToArray(),token);
        budget.Charge(progression.Work);
        var connections=source.Connections.ToList();var links=new List<MetadataRegionalTopologyLink>();var edgeIds=connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var result in topology.Results)
        foreach(var edge in result.Connections)
        {
            budget.Charge();var rule=rules[result.RuleId];var from=destinations[edge.FromId];var to=destinations[edge.ToId];
            string id="topology-"+MetadataExecutionCache.Hash(edge.Id)[..32];
            if(!edgeIds.Add(id))throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_CONNECTION_ID_CONFLICT");
            connections.Add(new(id,edge.Kind,rule.Topology.Purpose,rule.Topology.SourceRuleId,from.Endpoint,to.Endpoint,from.Endpoint.RegionId,
                rule.RoutingDomain??source.Surface.Boundary!,rule.Profile,rule.Search,edge.Bidirectional));links.Add(new(edge.Id,id));
            if(connections.Count>128)throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_CONNECTION_BUDGET");
        }
        return new(1,definition,nodes.ToArray(),topology,progression,source.Connections.ToArray(),connections.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray(),links.ToArray(),budget.Used-before);
    }
}
