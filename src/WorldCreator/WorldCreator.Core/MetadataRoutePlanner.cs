using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRouteAlignment(string ConnectionId,WorldPolyline Path,string SourceRuleId);
public sealed record MetadataRouteRule(string Id,string ConnectionKind,WorldTraversalProfile Profile,WorldRouteSearch Search,
    string? FromPortId=null,string? ToPortId=null,WorldRouteReuseSettings? Reuse=null,IReadOnlyList<MetadataInterfaceBinding>? Interfaces=null,
    IReadOnlyList<MetadataRouteAlignment>? Alignments=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorldPolygon? RoutingBoundary=null);
public sealed record MetadataRoutedConnection(string ConnectionId,string RuleId,WorldRouteResult Forward,WorldRouteResult? Reverse,MetadataInterfaceRoute? Interface=null);
public sealed record MetadataRouteBudget(string Purpose,string SourceRuleId,long MaximumWork=200000000,int MaximumDirections=128);
public sealed record MetadataRoutePlan(IReadOnlyList<MetadataRouteRule> Rules,IReadOnlyList<MetadataRoutedConnection> Routes,long AccessWork=0,long InterfaceWork=0,
    IReadOnlyList<string>? PreservedConnectionIds=null,long PreservationWork=0,MetadataRouteBudget? Budget=null,long Work=0);

/// <summary>Realizes only explicitly selected logical connections through the shared surface planner.</summary>
public sealed partial class MetadataRoutePlanner
{
    internal static MetadataRouteBudget NormalizeBudget(MetadataRouteBudget? budget)
    {
        budget??=new("Bounded aggregate route planning","engine:route-budget");
        if(!WorldEnvironmentQuery.Text(budget.Purpose)||!WorldEnvironmentQuery.Text(budget.SourceRuleId)||
            budget.MaximumWork is <1 or >200000000||budget.MaximumDirections is <1 or >128)
            throw new ArgumentException("METADATA_ROUTE_BUDGET_INVALID");
        return budget;
    }
    public MetadataRoutePlan Plan(IReadOnlyList<MetadataRouteRule> rules,WorldSurfaceDefinition surface,MetadataSpace space,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> connections,CancellationToken token=default,MetadataPhysicalAccessPlan? accessPlan=null,
        WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null,MetadataPlacementPlan? placement=null,WorldEnvironmentDefinition? environment=null,MetadataRouteBudget? budgetDefinition=null,WorldSurfaceDefinition? regionSurface=null)
        =>Plan(rules,surface,space,nodes,connections,token,accessPlan,null,regions,localLayouts,AllocationBindings(placement?.Definition),environment,placement?.Definition.SourceWorld?.RoutePlan,budgetDefinition,regionSurface,RegionBindings(placement?.Definition));

    internal MetadataRoutePlan Plan(IReadOnlyList<MetadataRouteRule> rules,WorldSurfaceDefinition surface,MetadataSpace space,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> connections,CancellationToken token,MetadataPhysicalAccessPlan? accessPlan,WorldSpatialWorkBudget? sharedBudget,
        WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null,IReadOnlyDictionary<string,string>? allocationBindings=null,WorldEnvironmentDefinition? environment=null,MetadataRoutePlan? preservedRoutes=null,MetadataRouteBudget? budgetDefinition=null,WorldSurfaceDefinition? regionSurface=null,IReadOnlyDictionary<string,string>? regionBindings=null,
        MetadataRouteMemo? memo=null,string phase="world-routes")
    {
        token.ThrowIfCancellationRequested();
        if(rules is null || rules.Count is <1 or >16)throw new ArgumentException("METADATA_ROUTE_RULES_INVALID");
        var constraint=new MetadataSurfaceConstraint(surface,space,token);
        var access=new MetadataPhysicalAccessPlanner();MetadataPhysicalAccessPlanner.ValidateSolidSet(surface,accessPlan,token);
        WorldSurfaceQuery? routeSurface=null;
        WorldSurfaceQuery SearchSurface()=>routeSurface??=access.RouteSurface(constraint.Query.Definition,accessPlan,token);
        var ruleSurfaces=new Dictionary<string,WorldSurfaceQuery>(StringComparer.Ordinal);
        WorldSurfaceQuery RuleSurface(MetadataRouteRule rule)=>ruleSurfaces.GetValueOrDefault(rule.Id)??SearchSurface();
        var geometryHashes=new Dictionary<string,string>(StringComparer.Ordinal);
        var declaredBudget=NormalizeBudget(budgetDefinition);
        var planningBudget=new WorldSpatialWorkBudget(declaredBudget.MaximumWork,token,sharedBudget);
        var accessBudget=new WorldSpatialWorkBudget(20000000,token,planningBudget);long interfaceWork=0,preservationWork=0;
        var preserved=new List<string>();
        var ids=new HashSet<string>(StringComparer.Ordinal);var kinds=new HashSet<string>(StringComparer.Ordinal);
        var definitions=new List<MetadataRouteRule>();var routes=new List<MetadataRoutedConnection>();
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        int routeCount=0;
        // Independent leg ceilings are not reservations. Actual charged work
        // shares one aggregate bank across access, interfaces and every route.
        foreach(var rule in rules)
        {
            token.ThrowIfCancellationRequested();
            if(rule is null || !SettlementCatalogRegistry.ValidId(rule.Id) || !ids.Add(rule.Id) ||
                !SettlementCatalogRegistry.ValidId(rule.ConnectionKind) || !kinds.Add(rule.ConnectionKind) ||
                rule.Search?.Waypoints is null || rule.Search.MaximumWork is <1 or >200000000)
                throw new ArgumentException("METADATA_ROUTE_RULE_INVALID");
            var selected=connections.Where(c=>c.Kind==rule.ConnectionKind).ToArray();
            if(selected.Length==0)throw new ArgumentException("METADATA_ROUTE_RULE_UNUSED:"+rule.Id);
            int count=selected.Sum(c=>c.Bidirectional?2:1);
            routeCount+=count;
            ValidateInterfaceBindings(rule,selected);
            if(rule.Alignments is {} alignments)
            {
                if(alignments.Count==0||alignments.Count>selected.Length||rule.Reuse is not null||rule.Interfaces is not null)
                    throw new ArgumentException("METADATA_ROUTE_ALIGNMENT_MODE_INVALID");
                var aligned=new HashSet<string>(StringComparer.Ordinal);
                foreach(var alignment in alignments)
                {
                    if(alignment is null||alignment.ConnectionId is null||!aligned.Add(alignment.ConnectionId)||!selected.Any(c=>c.Id==alignment.ConnectionId)||
                        string.IsNullOrWhiteSpace(alignment.SourceRuleId)||alignment.SourceRuleId.Length>2048||alignment.Path?.Points is null||
                        alignment.Path.Points.Count is <2 or >1024||alignment.Path.Points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
                        throw new ArgumentException("METADATA_ROUTE_ALIGNMENT_INVALID");
                    alignment.Path.Validate();
                }
            }
            if(routeCount>declaredBudget.MaximumDirections)throw new ArgumentException("METADATA_ROUTE_TOTAL_BUDGET");
            var profile=new WorldSurfaceTraversal(constraint.Query,rule.Profile,token).Profile;
            WorldSurfaceRoutePlanner.ValidateReuse(rule.Reuse);
            if(rule.RoutingBoundary is {} boundary)ruleSurfaces.Add(rule.Id,SearchSurface().RestrictTo(boundary,token));
            definitions.Add(NormalizeBindings(rule with{Profile=profile,Search=rule.Search with{Waypoints=Array.AsReadOnly(rule.Search.Waypoints.ToArray())}}));
        }
        InterfaceContext? interfaceContext=null;
        if(definitions.Any(r=>r.Interfaces is not null))
        {
            long before=planningBudget.Used;
            var domain=regionSurface is null?constraint.Query:new MetadataSurfaceConstraint(regionSurface,space,token).Query;
            if(!MetadataPopulationSources.Equal(domain.Definition with{Exclusions=[]},constraint.Query.Definition with{Exclusions=[]})||
                domain.Definition.Exclusions.Any(e=>!constraint.Query.Definition.Exclusions.Any(actual=>actual.Id==e.Id&&MetadataPopulationSources.Equal(actual,e))))
                throw new ArgumentException("METADATA_INTERFACE_REGION_SURFACE_MISMATCH");
            bool needLocal=definitions.Any(r=>r.Interfaces?.Any(b=>b.Source==MetadataInterfaceSource.LocalAllocation)==true);
            bool needRegion=definitions.Any(r=>r.Interfaces?.Any(b=>b.Source==MetadataInterfaceSource.RegionRelation)==true);
            interfaceContext=PrepareInterfaces(domain,regions,localLayouts,allocationBindings,regionBindings,environment,needLocal,needRegion,planningBudget,token);
            interfaceWork+=planningBudget.Used-before;
        }
        var reusable=definitions.ToDictionary(r=>r.Id,_=>new List<WorldRouteReusePath>(),StringComparer.Ordinal);
        var interfaceEnvironments=new Dictionary<string,WorldEnvironmentQuery>(StringComparer.Ordinal);
        WorldEnvironmentQuery InterfaceEnvironment(MetadataRouteRule rule)
        {
            if(rule.RoutingBoundary is null)return interfaceContext!.Environment;
            if(!interfaceEnvironments.TryGetValue(rule.Id,out var query))
                interfaceEnvironments.Add(rule.Id,query=new(interfaceContext!.Environment.Surface.RestrictTo(rule.RoutingBoundary,token),interfaceContext.Environment.Definition,token));
            return query;
        }
        foreach(var rule in definitions.OrderBy(r=>r.Id,StringComparer.Ordinal))
        foreach(var link in connections.Where(c=>c.Kind==rule.ConnectionKind).OrderBy(c=>c.Id,StringComparer.Ordinal))
        {
            WorldPoint Point(string id,string? portId)
            {
                if(!lookup.TryGetValue(id,out var node) || node.Position is not {} p || p.Count!=2)
                    throw new ArgumentException("METADATA_ROUTE_ENDPOINT_REQUIRED:"+id);
                return access.Endpoint(node,portId,accessPlan,surface,rule.Profile,accessBudget,token);
            }
            var from=Point(link.FromId,rule.FromPortId);var to=Point(link.ToId,rule.ToPortId);
            var alignment=rule.Alignments?.SingleOrDefault(a=>a.ConnectionId==link.Id);
            if(alignment is not null&&(alignment.Path.Points[0].DistanceTo(from)>1e-7||alignment.Path.Points[^1].DistanceTo(to)>1e-7))
                throw new MetadataSpatialConstraintException("METADATA_ROUTE_ALIGNMENT_ENDPOINT_REJECTED:"+link.Id);
            if(preservedRoutes?.Routes.SingleOrDefault(r=>r.ConnectionId==link.Id) is {} retained)
            {
                if(preservedRoutes.Rules.SingleOrDefault(r=>r.Id==retained.RuleId) is not {} originalRule||!MetadataPopulationSources.Equal(originalRule,rule)||
                    link.Bidirectional!=(retained.Reverse is not null))throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_RULE_CHANGED:"+rule.Id);
                long before=planningBudget.Used;
                void Check(WorldRouteResult route,WorldPoint start,WorldPoint end)
                {
                    if(alignment is not null)
                    {
                        var expected=start==from?alignment.Path.Points:alignment.Path.Points.Reverse().ToArray();
                        if(route.Path is null||!route.Path.Points.SequenceEqual(expected))throw new ArgumentException("METADATA_ROUTE_ALIGNMENT_PRESERVED_MISMATCH");
                        var checkedTrace=new WorldSurfaceRoutePlanner().Compile(RuleSurface(rule),new(route.Id,rule.Profile.Purpose,alignment.SourceRuleId,start,end,rule.Profile,rule.Search),route.Path,token,planningBudget);
                        if(checkedTrace.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_ROUTE_ALIGNMENT_REJECTED:"+link.Id);
                    }
                    else MetadataPopulationSources.ValidatePassage(route,start,end,rule.RoutingBoundary is null?constraint.Query:RuleSurface(rule),rule.Profile,link.Id,planningBudget,token,accessPlan);
                }
                Check(retained.Forward,from,to);if(retained.Reverse is {} backward)Check(backward,to,from);
                preservationWork+=planningBudget.Used-before;preserved.Add(link.Id);routes.Add(retained);
                if(rule.Reuse is not null)
                {
                    reusable[rule.Id].Add(new(retained.Forward.Id,retained.Forward.Path!,rule.Profile,link.Bidirectional));
                    if(retained.Reverse is {} reverse)reusable[rule.Id].Add(new(reverse.Id,reverse.Path!,rule.Profile,link.Bidirectional));
                }
                continue;
            }
            var binding=rule.Interfaces?.SingleOrDefault(b=>b.ConnectionId==link.Id);
            var domains=binding is null?null:ResolveInterface(binding,link,interfaceContext!);
            var interfaceEnvironment=binding is null?null:InterfaceEnvironment(rule);
            var existing=reusable[rule.Id];
            MetadataRoutedConnection ComputeConnection()
            {
                if(alignment is not null)
                {
                    WorldRouteResult Compile(bool reverse)
                    {
                        var result=new WorldSurfaceRoutePlanner().Compile(RuleSurface(rule),
                            new(link.Id+(reverse?"/reverse":"/forward"),rule.Profile.Purpose,alignment.SourceRuleId,reverse?to:from,reverse?from:to,rule.Profile,rule.Search),
                            reverse?new WorldPolyline(alignment.Path.Points.Reverse().ToArray()):alignment.Path,token,planningBudget);
                        if(result.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_ROUTE_ALIGNMENT_REJECTED:"+link.Id);
                        return result;
                    }
                    return new(link.Id,rule.Id,Compile(false),link.Bidirectional?Compile(true):null);
                }
                if(binding is not null)return RouteInterface(rule,binding,link,from,to,RuleSurface(rule),accessPlan,interfaceEnvironment!,domains!,existing,planningBudget,token);
                // Reverse construction sees its own qualified forward path. The
                // accepted outer source list changes once, equally on a hit/miss.
                var working=existing.ToList();
                WorldRouteResult Run(bool reverse)
                {
                    WorldRouteResult result;
                    try{result=new WorldSurfaceRoutePlanner().Plan(RuleSurface(rule),new(link.Id+(reverse?"/reverse":"/forward"),
                        rule.Profile.Purpose,link.SourceRuleId,reverse?to:from,reverse?from:to,rule.Profile,rule.Search,
                        rule.Reuse is {} reuse?new(reuse,working):null),token,planningBudget);}
                    catch(ArgumentException error)when(error.Message.Contains("BUDGET",StringComparison.Ordinal))
                    {
                        error.Data["ConnectionId"]=link.Id;error.Data["RouteRuleId"]=rule.Id;error.Data["Direction"]=reverse?"reverse":"forward";
                        throw;
                    }
                    if(result.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_ROUTE_UNRESOLVED:"+link.Id+":"+result.Status);
                    if(rule.Reuse is not null)working.Add(new(result.Id,result.Path!,rule.Profile,link.Bidirectional));
                    return result;
                }
                var forward=Run(false);return new(link.Id,rule.Id,forward,link.Bidirectional?Run(true):null);
            }
            long routeBefore=planningBudget.Used;
            MetadataRoutedConnection routed;
            if(memo is null)routed=ComputeConnection();
            else
            {
                string geometryKey=rule.RoutingBoundary is null?"":rule.Id;
                if(!geometryHashes.TryGetValue(geometryKey,out string? geometryHash))
                    geometryHashes.Add(geometryKey,geometryHash=MetadataExecutionCache.Hash(RuleSurface(rule).RouteDependencies(token)));
                object? domainInputs=binding is null?null:new{Domains=domains,Environment=interfaceEnvironment!.Definition};
                routed=memo.Run(nodes.Single(n=>n.ParentId is null).Id,rule.Id,link.Id,phase,
                    new{Rule=rule,Link=link,From=from,To=to,Space=space,Budget=declaredBudget,GeometrySha256=geometryHash,
                        Access=accessPlan?.Sites.Where(s=>s.NodeId==link.FromId||s.NodeId==link.ToId).OrderBy(s=>s.NodeId,StringComparer.Ordinal).ToArray(),
                        Domains=domainInputs,Existing=rule.Reuse is null?null:existing},ComputeConnection,planningBudget,token);
            }
            if(binding is not null)interfaceWork+=planningBudget.Used-routeBefore;
            routes.Add(routed);
            if(rule.Reuse is not null)
            {
                existing.Add(new(routed.Forward.Id,routed.Forward.Path!,rule.Profile,link.Bidirectional));
                if(routed.Reverse is {} reverse)existing.Add(new(reverse.Id,reverse.Path!,rule.Profile,link.Bidirectional));
            }
        }
        if(preservedRoutes is not null&&preserved.Count!=preservedRoutes.Routes.Count)throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_REMOVED");
        return new(Array.AsReadOnly(definitions.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()),routes.AsReadOnly(),accessBudget.Used,interfaceWork,
            preserved.Count==0?null:preserved.AsReadOnly(),preservationWork,declaredBudget,planningBudget.Used);
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        if(document.RoutePlan is not {} plan)return;
        if(document.Surface is null)throw new ArgumentException("METADATA_ROUTE_SURFACE_REQUIRED");
        var expected=Plan(plan.Rules,MetadataInteriorPlanner.WithWalls(document.Surface,document.InteriorPlan),document.Space,document.Nodes,document.Connections,token,document.AccessPlan,document.RegionPlan,document.LocalLayoutPlan,document.PlacementPlan,document.EnvironmentPlan?.Definition,plan.Budget,document.Surface);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_ROUTE_PLAN_MISMATCH");
    }
}
