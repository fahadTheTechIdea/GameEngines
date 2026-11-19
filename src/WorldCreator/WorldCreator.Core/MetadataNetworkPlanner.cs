using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataNetworkRule(string Id,string RegionNodeId,string DestinationKind,
    int MinimumDestinations,int MaximumDestinations,string ConnectionKind,WorldNetworkOptions Options,string? LayerId=null,string? PortId=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorldPolygon? RoutingBoundary=null);
public sealed record MetadataGeneratedNetwork(string RuleId,WorldDestinationNetworkPlan Plan);
public sealed record MetadataNetworkPlan(IReadOnlyList<MetadataNetworkRule> Rules,IReadOnlyList<MetadataGeneratedNetwork> Networks,long AccessWork=0,
    IReadOnlyList<string>? PreservedRuleIds=null,long PreservationWork=0);

/// <summary>Resolves scoped destination demand and publishes terrain-qualified logical edges.
/// Region/site positions remain explicit inputs until the placement stage supplies them.</summary>
public sealed partial class MetadataNetworkPlanner
{
    public MetadataNetworkPlan Plan(string worldId,IReadOnlyList<MetadataNetworkRule> rules,WorldSurfaceDefinition surface,
        MetadataSpace space,IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> authored,
        CancellationToken token=default,MetadataPhysicalAccessPlan? accessPlan=null,MetadataPlacementPlan? placement=null)
        =>Plan(worldId,rules,surface,space,nodes,authored,token,accessPlan,null,placement?.Definition.SourceWorld?.NetworkPlan);

    internal MetadataNetworkPlan Plan(string worldId,IReadOnlyList<MetadataNetworkRule> rules,WorldSurfaceDefinition surface,
        MetadataSpace space,IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> authored,
        CancellationToken token,MetadataPhysicalAccessPlan? accessPlan,WorldSpatialWorkBudget? sharedBudget,MetadataNetworkPlan? preservedNetworks=null,
        MetadataNetworkMemo? memo=null,string phase="world-network")
    {
        token.ThrowIfCancellationRequested();
        if(rules is null||rules.Count is <1 or >4)throw new ArgumentException("METADATA_NETWORK_RULES_INVALID");
        var constraint=new MetadataSurfaceConstraint(surface,space,token);constraint.ValidateNodes(nodes,token);
        var access=new MetadataPhysicalAccessPlanner();MetadataPhysicalAccessPlanner.ValidateSolidSet(surface,accessPlan,token);
        WorldSurfaceQuery? routeSurface=null;
        WorldSurfaceQuery SearchSurface()=>routeSurface??=access.RouteSurface(constraint.Query.Definition,accessPlan,token);
        string? geometryHash=null;
        var accessBudget=new WorldSpatialWorkBudget(20000000,token,sharedBudget);
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
        var kinds=authored.Select(c=>c.Kind).ToHashSet(StringComparer.Ordinal);long budget=0;
        foreach(var rule in rules)
        {
            if(rule is null||!SettlementCatalogRegistry.ValidId(rule.Id)||!ids.Add(rule.Id)||
                !SettlementCatalogRegistry.ValidId(rule.DestinationKind)||!SettlementCatalogRegistry.ValidId(rule.ConnectionKind)||!kinds.Add(rule.ConnectionKind)||
                rule.RegionNodeId is null||!lookup.ContainsKey(rule.RegionNodeId)||rule.MinimumDestinations<2||rule.MaximumDestinations>32||
                rule.MaximumDestinations<rule.MinimumDestinations||rule.Options is null||rule.Options.MaximumWork is <1 or >200000000)
                throw new ArgumentException("METADATA_NETWORK_RULE_INVALID");
            budget+=rule.Options.MaximumWork;if(budget>200000000)throw new ArgumentException("METADATA_NETWORK_TOTAL_BUDGET");
        }
        var definitions=new List<MetadataNetworkRule>();var networks=new List<MetadataGeneratedNetwork>();
        var preserved=new List<string>();long preservationWork=0;
        foreach(var rule in rules.OrderBy(r=>r.Id,StringComparer.Ordinal))
        {
            bool InScope(WorldMetadataNode node)
            {
                var visited=new HashSet<string>(StringComparer.Ordinal);string? id=node.ParentId;
                while(id is not null)
                {
                    token.ThrowIfCancellationRequested();
                    if(!visited.Add(id)||visited.Count>32||!lookup.TryGetValue(id,out var parent))throw new ArgumentException("METADATA_NETWORK_HIERARCHY_INVALID");
                    if(id==rule.RegionNodeId)return true;id=parent.ParentId;
                }
                return false;
            }
            var selected=nodes.Where(n=>n.Kind==rule.DestinationKind&&InScope(n)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            if(selected.Length<rule.MinimumDestinations||selected.Length>rule.MaximumDestinations)
                throw new ArgumentException("METADATA_NETWORK_DESTINATION_COUNT:"+rule.Id);
            var retained=preservedNetworks?.Networks.SingleOrDefault(n=>n.RuleId==rule.Id);
            if(retained is not null&&!selected.Select(n=>(n.Id,n.Purpose)).SequenceEqual(retained.Plan.Destinations.Select(d=>(d.Id,d.Purpose))))
                throw new ArgumentException("METADATA_POPULATION_SOURCE_NETWORK_DESTINATIONS_CHANGED:"+rule.Id);
            var destinations=selected.Select(n=>n.Position is {Count:2} p?new WorldNetworkDestination(n.Id,n.Purpose,access.Endpoint(n,rule.PortId,accessPlan,surface,rule.Options.Profile,accessBudget,token)):
                throw new ArgumentException("METADATA_NETWORK_POSITION_REQUIRED:"+n.Id)).ToArray();
            var regionalQuery=rule.RoutingBoundary is {} region?SearchSurface().RestrictTo(region,token):null;
            if(retained is not null)
            {
                var canonical=CanonicalRule(rule);
                if(preservedNetworks!.Rules.SingleOrDefault(r=>r.Id==rule.Id) is not {} original||!MetadataPopulationSources.Equal(original,canonical))
                    throw new ArgumentException("METADATA_POPULATION_SOURCE_NETWORK_RULE_CHANGED:"+rule.Id);
                if(!MetadataPopulationSources.Equal(retained.Plan.Destinations,destinations))
                    throw new ArgumentException("METADATA_POPULATION_SOURCE_NETWORK_DESTINATIONS_CHANGED:"+rule.Id);
                var preservationBudget=new WorldSpatialWorkBudget(rule.Options.MaximumWork,token,sharedBudget);
                var endpoints=destinations.ToDictionary(d=>d.Id,d=>d.Position,StringComparer.Ordinal);
                foreach(var edge in retained.Plan.Edges)
                {
                    MetadataPopulationSources.ValidatePassage(edge.Forward,endpoints[edge.Link.FromId],endpoints[edge.Link.ToId],regionalQuery??constraint.Query,
                        rule.Options.Profile,edge.Link.Id,preservationBudget,token,accessPlan);
                    MetadataPopulationSources.ValidatePassage(edge.Reverse,endpoints[edge.Link.ToId],endpoints[edge.Link.FromId],regionalQuery??constraint.Query,
                        rule.Options.Profile,edge.Link.Id,preservationBudget,token,accessPlan);
                }
                preservationWork+=preservationBudget.Used;preserved.Add(rule.Id);definitions.Add(canonical);networks.Add(retained);
                continue;
            }
            var query=regionalQuery??SearchSurface();
            WorldDestinationNetworkPlan Compute()=>new WorldDestinationNetworkPlanner().Plan(query,new(worldId+"/network/"+rule.Id,destinations,rule.Options),token,sharedBudget);
            WorldDestinationNetworkPlan plan;
            if(memo is null)plan=Compute();
            else
            {
                string queryHash=regionalQuery is not null?MetadataExecutionCache.Hash(query.RouteDependencies(token)):
                    geometryHash??=MetadataExecutionCache.Hash(query.RouteDependencies(token));
                plan=memo.Run(worldId,rule.Id,phase,new{worldId,Rule=CanonicalRule(rule),destinations,space,GeometrySha256=queryHash},Compute,sharedBudget,token);
            }
            if(plan.Status!=WorldNetworkStatus.Found)throw new MetadataSpatialConstraintException("METADATA_NETWORK_UNRESOLVED:"+rule.Id+":"+plan.Status);
            definitions.Add(rule with{Options=plan.Options});networks.Add(new(rule.Id,plan));
        }
        if(preservedNetworks is not null&&preserved.Count!=preservedNetworks.Networks.Count)throw new ArgumentException("METADATA_POPULATION_SOURCE_NETWORK_REMOVED");
        return new(definitions.AsReadOnly(),networks.AsReadOnly(),accessBudget.Used,preserved.Count==0?null:preserved.AsReadOnly(),preservationWork);
    }

    internal static MetadataNetworkRule CanonicalRule(MetadataNetworkRule rule)
    {
        var pairs=rule.Options.RequiredConnections??[];
        if(pairs.Count>496||pairs.Any(p=>p is null||p.FromId is null||p.ToId is null))throw new ArgumentException("WORLD_NETWORK_REQUIRED_INVALID");
        var forbidden=rule.Options.ForbiddenConnections;
        if(forbidden?.Count>496||forbidden?.Any(p=>p is null||p.FromId is null||p.ToId is null)==true)throw new ArgumentException("WORLD_NETWORK_FORBIDDEN_INVALID");
        WorldRequiredConnection[] Normalize(IEnumerable<WorldRequiredConnection> source)=>source.Select(p=>StringComparer.Ordinal.Compare(p.FromId,p.ToId)<=0?p:new WorldRequiredConnection(p.ToId,p.FromId))
            .OrderBy(p=>p.FromId,StringComparer.Ordinal).ThenBy(p=>p.ToId,StringComparer.Ordinal).ToArray();
        return rule with{Options=rule.Options with{RequiredConnections=Normalize(pairs),ForbiddenConnections=forbidden is null?null:Normalize(forbidden)}};
    }

    public IReadOnlyList<WorldMetadataConnection> Connections(MetadataNetworkPlan plan)
    {
        var rules=plan.Rules.ToDictionary(r=>r.Id,StringComparer.Ordinal);
        return Array.AsReadOnly(plan.Networks.SelectMany(n=>n.Plan.Edges.Select(e=>new WorldMetadataConnection(e.Link.Id,
            rules[n.RuleId].ConnectionKind,e.Link.FromId,e.Link.ToId,true,n.Plan.Options.SourceRuleId,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string,JsonElement>(new Dictionary<string,JsonElement>()),rules[n.RuleId].LayerId)))
            .OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray());
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        string prefix=document.Id+"/network/";
        if(document.NetworkPlan is not {} plan)
        {
            if(document.Connections.Any(c=>c.Id.StartsWith(prefix,StringComparison.Ordinal)))throw new ArgumentException("METADATA_NETWORK_PLAN_REQUIRED");
            return;
        }
        if(document.Surface is null)throw new ArgumentException("METADATA_NETWORK_SURFACE_REQUIRED");
        var authored=document.Connections.Where(c=>!c.Id.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
        if(document.RoutePlan?.Rules.Any(r=>plan.Rules.Any(n=>n.ConnectionKind==r.ConnectionKind))==true)
            throw new ArgumentException("METADATA_NETWORK_ROUTE_KIND_CONFLICT");
        var expected=Plan(document.Id,plan.Rules,MetadataInteriorPlanner.WithWalls(document.Surface,document.InteriorPlan),document.Space,document.Nodes,authored,token,document.AccessPlan,document.PlacementPlan);
        var actual=document.Connections.Where(c=>c.Id.StartsWith(prefix,StringComparison.Ordinal)).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
        string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
        if(Json(expected)!=Json(plan)||Json(Connections(expected))!=Json(actual))throw new ArgumentException("METADATA_NETWORK_PLAN_MISMATCH");
    }
}
