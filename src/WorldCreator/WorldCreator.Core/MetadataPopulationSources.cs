using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>The same generator executes this base phase before the complete population recipe.
/// One explicit source phase is supported; recursive sources are refused.</summary>
public sealed record MetadataPopulationSource(WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationContext? Context=null);
/// <summary>Explicitly inherit the caller's surface, origin and named root anchors.
/// The source template must not override those spatial facts.</summary>
public sealed record MetadataPopulationContext(IReadOnlyList<string> PlacementAnchorIds,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool UsePlacementBoundary=false);
public sealed record MetadataParentRouteSelector(string ConnectionKind,int ConnectionIndex=0);
public sealed record MetadataGeneratedRoutePattern(string? ConnectionId,double Spacing,double StartDistance=0,
    double EndInset=0,double NormalOffset=0,bool Reverse=false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataParentRouteSelector? Parent=null);
public sealed record MetadataGeneratedRegionPattern(string RegionId,WorldRegionPopulationPattern? Pattern=null,string? AllocationId=null,WorldContourPopulationPattern? Contour=null,WorldAreaPopulationPattern? Area=null);

internal static partial class MetadataPopulationSources
{
    internal static IEnumerable<(string Id,WorldRouteResult Forward,WorldRouteResult? Reverse,WorldTraversalProfile Profile)> Passages(WorldMetadataDocument source)
    {
        foreach(var interior in source.InteriorPlan?.Results??[])
        foreach(var route in interior.Routes)
            yield return(route.ConnectionId,route.Forward,route.Reverse,source.InteriorPlan!.Definition.Rules.Single(r=>r.Id==interior.RuleId).Profile);
        foreach(var route in source.RoutePlan?.Routes??[])
            yield return(route.ConnectionId,route.Forward,route.Reverse,source.RoutePlan!.Rules.Single(r=>r.Id==route.RuleId).Profile);
        foreach(var network in source.NetworkPlan?.Networks??[])
        foreach(var edge in network.Plan.Edges)
            yield return(edge.Link.Id,edge.Forward,edge.Reverse,network.Plan.Options.Profile);
    }
    internal static void ValidatePassage(WorldRouteResult route,WorldPoint start,WorldPoint end,WorldSurfaceQuery surface,
        WorldTraversalProfile profile,string id,WorldSpatialWorkBudget budget,CancellationToken token,MetadataPhysicalAccessPlan? access=null)
    {
        if(route.Status!=WorldRouteStatus.Found||route.Path is not {} path||path.Points.Count<2||path.Points[0]!=start||path.Points[^1]!=end)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_ENDPOINT_CHANGED:"+id);
        var traversal=new WorldSurfaceTraversal(surface,profile,token);
        for(int i=1;i<path.Points.Count;i++)
            if(traversal.Evaluate(path.Points[i-1],path.Points[i],budget,token) is null||
                MetadataPhysicalAccessPlanner.IntersectsSolids(access,path.Points[i-1],path.Points[i],profile.Radius,budget,token))
                throw new MetadataSpatialConstraintException("METADATA_POPULATION_PASSAGE_BLOCKED:"+id);
    }
    internal static void ReservePassages(WorldMetadataDocument source,IReadOnlyList<MetadataContextGeometry> geometry,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var baseIds=source.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        var shapes=geometry.Where(g=>!baseIds.Contains(g.NodeId)).SelectMany(g=>new[]{g.Footprint}.Concat(g.Surroundings)
            .DistinctBy(MetadataExecutionCache.Hash).Select(p=>(g.NodeId,Boundary:p))).ToArray();
        if(shapes.Length==0)return;
        void Check(WorldPolyline path,double radius,string id)
        {
            foreach(var shape in shapes)
            for(int i=1;i<path.Points.Count;i++)
                if(WorldSurfaceQuery.IntersectsExcludedPolygon(shape.Boundary,path.Points[i-1],path.Points[i],radius,budget,token))
                {
                    var error=new MetadataSpatialConstraintException("METADATA_POPULATION_PASSAGE_BLOCKED:"+id+":member="+shape.NodeId);
                    error.Data["PopulationPassageConflict"]=new{ConnectionId=id,shape.NodeId,shape.Boundary,From=path.Points[i-1],To=path.Points[i],Radius=radius};
                    throw error;
                }
        }
        foreach(var route in Passages(source))
        {
            Check(route.Forward.Path!,route.Profile.Radius,route.Id);if(route.Reverse is {} reverse)Check(reverse.Path!,route.Profile.Radius,route.Id);
        }
        foreach(var site in source.AccessPlan?.Sites??[])
        foreach(var port in site.Ports)Check(new([port.BoundaryPosition,port.ApproachPosition]),port.Width/2,site.NodeId+":"+port.Id);
    }
    internal static bool Equal<T>(T first,T second)=>System.Text.Json.JsonElement.DeepEquals(
        System.Text.Json.JsonSerializer.SerializeToElement(first,WorldJson.Shared),
        System.Text.Json.JsonSerializer.SerializeToElement(second,WorldJson.Shared));
    internal static void ValidateScope(WorldMetadataDocument source)
    {
        if(source.PlacementPlan?.Definition.SourceWorld is not null||source.PlacementPlan?.TemplateDefinition?.SourceWorld is not null||
            (!Passages(source).Any()&&source.RegionPlan?.Regions is not {Count:>0}&&source.ContextPlan?.Protections is not {Count:>0}&&
                source.PlacementPlan?.SiteFeatures is not {Count:>0}&&source.GroundCoverPlan is null)||source.RoutePlan?.PreservedConnectionIds is not null||source.NetworkPlan?.PreservedRuleIds is not null)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_UNSUPPORTED");
    }
    internal static void ValidateSource(WorldMetadataDocument source,WorldSurfaceDefinition surface,MetadataSpace space,
        IReadOnlyList<WorldMetadataNode> nodes,MetadataPlacementFeedback? feedback,ISettlementCatalogRegistry? catalogs,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ValidateScope(source);
        WorldMetadataGenerator.ValidateDocument(source,catalogs,token);
        if(!Equal(source.Surface,surface)||!Equal(source.Space,space))throw new ArgumentException("METADATA_POPULATION_SOURCE_SURFACE_CHANGED");
        ValidateContext(source.ContextPlan?.Definition,feedback?.Context);
        if(source.InteriorPlan is {} interior&&!Equal(interior.Definition,feedback?.Interiors))
            throw new ArgumentException("METADATA_POPULATION_SOURCE_INTERIOR_RULE_CHANGED");
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var node in source.Nodes)
        {
            token.ThrowIfCancellationRequested();
            if(!lookup.TryGetValue(node.Id,out var current)||!Equal(node with{Position=null},current with{Position=null}))
                throw new ArgumentException("METADATA_POPULATION_SOURCE_MEMBER_CHANGED:"+node.Id);
        }
        foreach(var rule in source.RoutePlan?.Rules??[])
            if(feedback?.RouteRules?.SingleOrDefault(r=>r.Id==rule.Id) is not {} current||!Equal(rule,current))
                throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_RULE_CHANGED:"+rule.Id);
        foreach(var rule in source.NetworkPlan?.Rules??[])
            if(feedback?.NetworkRules?.SingleOrDefault(r=>r.Id==rule.Id) is not {} current||!Equal(rule,MetadataNetworkPlanner.CanonicalRule(current)))
                throw new ArgumentException("METADATA_POPULATION_SOURCE_NETWORK_RULE_CHANGED:"+rule.Id);
        var generated=(source.NetworkPlan?.Networks.SelectMany(n=>n.Plan.Edges.Select(e=>e.Link.Id))??[]).ToHashSet(StringComparer.Ordinal);
        foreach(var link in source.Connections.Where(c=>!generated.Contains(c.Id)))
            if(feedback?.AuthoredConnections.SingleOrDefault(c=>c.Id==link.Id) is not {} current||!Equal(link,current))
                throw new ArgumentException("METADATA_POPULATION_SOURCE_CONNECTION_CHANGED:"+link.Id);
    }
    internal static IReadOnlyList<WorldPoint> Positions(MetadataGeneratedRoutePattern pattern,int count,WorldMetadataDocument? source,string? parent,WorldSpatialWorkBudget budget,CancellationToken token)
        =>WorldPopulationGeometry.Path(RoutePath(pattern,source,parent,budget),count,token);

    internal static WorldPathPattern RoutePath(MetadataGeneratedRoutePattern pattern,WorldMetadataDocument? source,string? parent,WorldSpatialWorkBudget budget)
    {
        if(source is null)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_MISSING");
        string? connectionId=pattern.ConnectionId;bool reverse=pattern.Reverse;
        if(pattern.Parent is {} selector)
        {
            if(connectionId is not null||!WorldEnvironmentQuery.Id(selector.ConnectionKind)||selector.ConnectionIndex is <0 or >127)
                throw new ArgumentException("METADATA_POPULATION_PARENT_ROUTE_SELECTOR_INVALID");
            budget.Charge(source.Nodes.Count);
            if(parent is null||!source.Nodes.Any(n=>n.Id==parent))throw new ArgumentException("METADATA_POPULATION_PARENT_ROUTE_CONTEXT_REQUIRED");
            // Keep only the requested ordinal prefix, never sort/store the entire
            // incident graph. Selection uses IDs, not input order or spatial vacancy.
            var prefix=new SortedSet<WorldMetadataConnection>(Comparer<WorldMetadataConnection>.Create((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id)));
            foreach(var link in source.Connections)
            {
                budget.Charge();
                if(link.Kind!=selector.ConnectionKind||(link.FromId!=parent&&link.ToId!=parent))continue;
                budget.Charge(1+(long)Math.Ceiling(Math.Log2(selector.ConnectionIndex+2)));
                if(!prefix.Add(link))throw new ArgumentException("METADATA_POPULATION_PARENT_ROUTE_AMBIGUOUS");
                if(prefix.Count>selector.ConnectionIndex+1)prefix.Remove(prefix.Max!);
            }
            if(prefix.Count<=selector.ConnectionIndex)throw new ArgumentException("METADATA_POPULATION_PARENT_ROUTE_MISSING");
            var selected=prefix.Max!;connectionId=selected.Id;
            // A source reverse trace is mandatory when its canonical destination
            // is the parent. The explicit Reverse flag flips this relative choice.
            reverse^=selected.ToId==parent;
        }
        else if(connectionId is null)throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_MISSING");
        var matches=new List<(string Id,WorldRouteResult Forward,WorldRouteResult? Reverse,WorldTraversalProfile Profile)>();
        foreach(var passage in Passages(source))
        {
            if(pattern.Parent is not null)budget.Charge();
            if(passage.Id==connectionId)matches.Add(passage);
        }
        if(matches.Count!=1)throw new ArgumentException("METADATA_POPULATION_SOURCE_ROUTE_MISSING");
        var route=matches[0];
        var selectedRoute=reverse?route.Reverse:route.Forward;
        if(selectedRoute?.Path is null)throw new ArgumentException("METADATA_POPULATION_SOURCE_DIRECTION_MISSING");
        var origin=ParentOrigin(source,parent);
        var path=new WorldPolyline(selectedRoute.Path.Points.Select(p=>new WorldPoint(p.X-origin.X,p.Y-origin.Y)).ToArray());
        return new(path,pattern.Spacing,pattern.StartDistance,pattern.EndInset,pattern.NormalOffset);
    }
    internal static IReadOnlyList<WorldPoint> Positions(MetadataGeneratedRegionPattern pattern,int count,WorldMetadataDocument? source,string? parent,WorldSpatialWorkBudget budget,CancellationToken token,
        Action<IReadOnlyList<WorldContourPopulationResult>>? recordContours=null,Action<WorldAreaPopulationPlan>? recordArea=null)
    {
        if(pattern.RegionId is null||source?.RegionPlan?.Regions.SingleOrDefault(r=>r.Id==pattern.RegionId) is not {} region)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_REGION_MISSING");
        if(pattern.AllocationId is {} id)
        {
            var allocation=source.LocalLayoutPlan?.Allocations.SingleOrDefault(a=>a.Id==id&&a.RegionId==pattern.RegionId)
                ??throw new ArgumentException("METADATA_POPULATION_SOURCE_ALLOCATION_MISSING");
            region=allocation.Shape;
        }
        if((pattern.Pattern is null?0:1)+(pattern.Contour is null?0:1)+(pattern.Area is null?0:1)!=1)throw new ArgumentException("METADATA_POPULATION_REGION_GRAMMAR_INVALID");
        IReadOnlyList<WorldPoint> positions;
        if(pattern.Pattern is {} bands)positions=WorldPopulationGeometry.Region(region,bands,count,budget,token);
        else if(pattern.Contour is not null)
        {
            var contours=WorldPopulationGeometry.Contour(region,new WorldEnvironmentQuery(new WorldSurfaceQuery(source.Surface!,token),
                source.EnvironmentPlan?.Definition??throw new ArgumentException("METADATA_POPULATION_CONTOUR_ENVIRONMENT_REQUIRED"),token),pattern.Contour!,count,budget,token);
            recordContours?.Invoke(contours);positions=contours.SelectMany(r=>r.Positions).ToArray();
        }
        else
        {
            var plan=WorldPopulationGeometry.Area(new WorldAreaPopulationDomain(source.RegionPlan,pattern.RegionId,
                pattern.AllocationId is null?null:source.LocalLayoutPlan,pattern.AllocationId),new WorldEnvironmentQuery(new WorldSurfaceQuery(source.Surface!,token),
                source.EnvironmentPlan?.Definition??throw new ArgumentException("METADATA_POPULATION_AREA_ENVIRONMENT_REQUIRED"),token),
                pattern.Area!,count,budget,token);
            recordArea?.Invoke(plan);positions=plan.Strata.Select(s=>s.Position).ToArray();
        }
        var origin=ParentOrigin(source,parent);
        return Array.AsReadOnly(positions.Select(p=>new WorldPoint(p.X-origin.X,p.Y-origin.Y)).ToArray());
    }
    internal static WorldPoint ParentOrigin(WorldMetadataDocument source,string? parent)
    {
        if(parent is not null)
        {
            var node=source.Nodes.SingleOrDefault(n=>n.Id==parent);
            if(node?.Position is not {Count:2} position)throw new ArgumentException("METADATA_POPULATION_SOURCE_PARENT_MISSING");
            return new(position[0],position[1]);
        }
        return new(0,0);
    }
    internal static void ValidateResult(WorldMetadataDocument source,WorldMetadataDocument world,CancellationToken token)
    {
        if(source.Id!=world.Id||!Equal(source.Surface,world.Surface)||!Equal(source.Space,world.Space))throw new ArgumentException("METADATA_POPULATION_BASE_CHANGED");
        if(source.DrainagePlan is not null&&!Equal(source.DrainagePlan,world.DrainagePlan))throw new ArgumentException("METADATA_POPULATION_BASE_DRAINAGE_CHANGED");
        if(source.WaterPlan is not null&&!Equal(source.WaterPlan,world.WaterPlan))throw new ArgumentException("METADATA_POPULATION_BASE_WATER_CHANGED");
        if(source.WindMoisturePlan is not null&&!Equal(source.WindMoisturePlan,world.WindMoisturePlan))throw new ArgumentException("METADATA_POPULATION_BASE_WIND_MOISTURE_CHANGED");
        ValidateContext(source.ContextPlan?.Definition,world.ContextPlan?.Definition);
        if(source.InteriorPlan is {} interior&&(world.InteriorPlan is not {} currentInterior||
            !Equal(interior.Definition,currentInterior.Definition)||!Equal(interior.Results,currentInterior.Results)))
            throw new ArgumentException("METADATA_POPULATION_BASE_INTERIOR_CHANGED");
        foreach(var allocation in source.VariationPlan?.Allocations??[])
            if(world.VariationPlan?.Allocations.SingleOrDefault(a=>a.Key==allocation.Key) is not {} choice||!Equal(allocation,choice))
                throw new ArgumentException("METADATA_POPULATION_BASE_VARIATION_CHANGED:"+allocation.Key);
        foreach(var protection in source.ContextPlan?.Protections??[])
            if(world.ContextPlan?.Protections?.SingleOrDefault(p=>p.Definition.Id==protection.Definition.Id) is not {} current||!Equal(protection,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_PROTECTION_CHANGED:"+protection.Definition.Id);
        if(source.RegionPlan is not null&&!Equal(source.RegionPlan,world.RegionPlan)||source.LocalLayoutPlan is not null&&!Equal(source.LocalLayoutPlan,world.LocalLayoutPlan)||
            source.EnvironmentPlan is not null&&!Equal(source.EnvironmentPlan.Definition,world.EnvironmentPlan?.Definition))
            throw new ArgumentException("METADATA_POPULATION_BASE_SPATIAL_PLAN_CHANGED");
        foreach(var node in source.Nodes)
        {
            token.ThrowIfCancellationRequested();
            if(world.Nodes.SingleOrDefault(n=>n.Id==node.Id) is not {} current||!Equal(node,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_NODE_CHANGED:"+node.Id);
        }
        foreach(var site in source.AccessPlan?.Sites??[])
            if(world.AccessPlan?.Sites.SingleOrDefault(s=>s.NodeId==site.NodeId) is not {} current||!Equal(site,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_PHYSICAL_CHANGED:"+site.NodeId);
        var currentFootprints=(world.PlacementPlan?.Footprints??[]).Concat(world.PlacementPlan?.NestedStages.SelectMany(s=>s.Footprints)??[]).ToDictionary(f=>f.NodeId);
        foreach(var footprint in (source.PlacementPlan?.Footprints??[]).Concat(source.PlacementPlan?.NestedStages.SelectMany(s=>s.Footprints)??[]))
            if(!currentFootprints.TryGetValue(footprint.NodeId,out var current)||!Equal(footprint,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_SOURCE_GEOMETRY_CHANGED:"+footprint.NodeId);
        foreach(var definition in source.StructureDefinitions)
            if(!world.StructureDefinitions.TryGetValue(definition.Key,out var current)||!Equal(definition.Value,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_STRUCTURE_CHANGED:"+definition.Key);
        foreach(var link in source.Connections)
            if(world.Connections.SingleOrDefault(c=>c.Id==link.Id) is not {} current||!Equal(link,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_CONNECTION_CHANGED:"+link.Id);
        foreach(var network in source.NetworkPlan?.Networks??[])
            if(world.NetworkPlan?.Networks.SingleOrDefault(n=>n.RuleId==network.RuleId) is not {} current||!Equal(network,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_NETWORK_CHANGED:"+network.RuleId);
        foreach(var route in source.RoutePlan?.Routes??[])
            if(world.RoutePlan?.Routes.SingleOrDefault(r=>r.ConnectionId==route.ConnectionId) is not {} current||!Equal(route,current))
                throw new ArgumentException("METADATA_POPULATION_BASE_ROUTE_CHANGED:"+route.ConnectionId);
    }
    private static void ValidateContext(MetadataContextDefinition? source,MetadataContextDefinition? current)
    {
        if(source is null)return;
        void Preserve<T>(IEnumerable<T> original,IEnumerable<T>? final,Func<T,string> id)
        {
            var lookup=(final??[]).ToDictionary(id,StringComparer.Ordinal);
            foreach(var value in original)
                if(!lookup.TryGetValue(id(value),out var found)||!Equal(value,found))throw new ArgumentException("METADATA_POPULATION_SOURCE_CONTEXT_CHANGED:"+id(value));
        }
        Preserve(source.Regions,current?.Regions,r=>r.Id);
        Preserve(source.Sites,current?.Sites,s=>s.NodeId);
        Preserve(source.Separations,current?.Separations,s=>s.Id);
        Preserve(source.Exclusions??[],current?.Exclusions,e=>e.Id);
        Preserve(source.Protections??[],current?.Protections,p=>p.Id);
    }
}
