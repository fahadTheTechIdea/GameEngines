using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataInterfaceSource { LocalAllocation, RegionRelation }
public sealed record MetadataInterfaceBinding(string ConnectionId,string InterfaceId,int MaximumCrossingCandidates=3,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] MetadataInterfaceSource Source=MetadataInterfaceSource.LocalAllocation);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataInterfaceJoinCost(double Ascent,double Descent);
public sealed record MetadataInterfaceJourney(WorldRouteResult FromLeg,WorldRouteResult Crossing,WorldRouteResult ToLeg,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataInterfaceJoinCost? JoinCost=null);
public sealed record MetadataInterfaceAttempt(int Ordinal,WorldPoint Center,bool Accepted,string? Rejection,long Work);
public sealed record MetadataInterfaceRoute(string InterfaceId,string FromDomainId,string ToDomainId,WorldRegionEdge Span,
    WorldPoint Center,MetadataInterfaceJourney Forward,MetadataInterfaceJourney? Reverse,IReadOnlyList<MetadataInterfaceAttempt> Attempts);

public sealed partial class MetadataRoutePlanner
{
    internal static void ValidateInterfaceEntrance(MetadataAccessPort port,string nodeId,WorldRegion shape,
        WorldEnvironmentQuery environment,WorldSuitabilityProfile profile,WorldTraversalProfile movement,long maximumWork,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var envelope=WorldSurfaceTraversal.Envelope(port.BoundaryPosition,port.ApproachPosition,movement.Radius);
        if(!WorldRegionGeometry.Contains(shape,envelope,budget)||
            !new WorldFootprintSuitability(environment,profile).Evaluate(envelope,maximumWork,4096,budget,token).Eligible)
            throw new MetadataSpatialConstraintException("METADATA_INTERFACE_ENTRANCE_OUTSIDE_DOMAIN:"+nodeId);
    }
    private sealed record InterfaceContext(WorldEnvironmentQuery Environment,WorldRegionPlan Regions,WorldLocalLayoutPlan? Layouts,
        IReadOnlyDictionary<string,string> AllocationBindings,IReadOnlyDictionary<string,string> RegionBindings);

    internal static IReadOnlyDictionary<string,string> AllocationBindings(MetadataPlacementDefinition? placement)=>DomainBindings(placement,false);
    internal static IReadOnlyDictionary<string,string> RegionBindings(MetadataPlacementDefinition? placement)=>DomainBindings(placement,true);
    private static IReadOnlyDictionary<string,string> DomainBindings(MetadataPlacementDefinition? placement,bool regions)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);if(placement is null)return result;
        var parents=new Dictionary<string,string?>(StringComparer.Ordinal);
        var stages=new[]{(placement.Request,placement.Bindings,Parent:(string?)null)}
            .Concat((placement.NestedStages??[]).Select(s=>(s.Request,s.Bindings,Parent:(string?)s.ParentNodeId)));
        foreach(var (request,bindings,parent) in stages)foreach(var binding in bindings)
        {
            if(!parents.TryAdd(binding.NodeId,parent))throw new ArgumentException("METADATA_INTERFACE_NODE_DOMAIN_CONFLICT");
            var demand=request.Demands.SingleOrDefault(d=>d.Id==binding.DemandId);
            if((regions?demand?.RegionId:demand?.AllocationId) is {} id&&!result.TryAdd(binding.NodeId,id))throw new ArgumentException("METADATA_INTERFACE_NODE_DOMAIN_CONFLICT");
        }
        // A nested physical endpoint belongs to its nearest explicitly allocated ancestor.
        // Resolve from declared stage ownership, independent of declaration order. This does
        // not exempt the child footprint or its approach from geometric containment checks.
        foreach(var node in parents.Keys.OrderBy(id=>id,StringComparer.Ordinal))
        {
            var seen=new HashSet<string>(StringComparer.Ordinal);string? owner=node;string? allocation=null;
            while(owner is not null)
            {
                if(!seen.Add(owner)||seen.Count>33||!parents.TryGetValue(owner,out var parent))
                    throw new ArgumentException("METADATA_INTERFACE_DOMAIN_HIERARCHY_INVALID");
                if(allocation is null&&result.TryGetValue(owner,out var selected))allocation=selected;
                owner=parent;
            }
            if(allocation is not null)result.TryAdd(node,allocation);
        }
        return result;
    }

    internal static void ValidateInterfaceBindings(MetadataRouteRule rule,IReadOnlyList<WorldMetadataConnection> selected)
    {
        if(rule.Interfaces is null)return;
        if(rule.Interfaces.Count is <1 or >16||rule.FromPortId is null||rule.ToPortId is null)throw new ArgumentException("METADATA_INTERFACE_BINDINGS_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var binding in rule.Interfaces)
            if(binding is null||!WorldEnvironmentQuery.Text(binding.ConnectionId)||!ids.Add(binding.ConnectionId)||!WorldEnvironmentQuery.Id(binding.InterfaceId)||
                binding.MaximumCrossingCandidates is <1 or >16||!Enum.IsDefined(binding.Source)||!selected.Any(c=>c.Id==binding.ConnectionId))throw new ArgumentException("METADATA_INTERFACE_BINDING_INVALID");
    }

    internal static MetadataRouteRule NormalizeBindings(MetadataRouteRule rule)
    {
        if(rule.Interfaces?.Any(b=>b is null)==true)throw new ArgumentException("METADATA_INTERFACE_BINDINGS_INVALID");
        if(rule.Alignments?.Any(a=>a?.Path?.Points is null)==true)throw new ArgumentException("METADATA_ROUTE_ALIGNMENT_INVALID");
        return rule with{RoutingBoundary=rule.RoutingBoundary is {Vertices:{} vertices}?new WorldPolygon(Array.AsReadOnly(vertices.ToArray())):rule.RoutingBoundary,
            Interfaces=rule.Interfaces is null?null:Array.AsReadOnly(rule.Interfaces.OrderBy(b=>b.ConnectionId,StringComparer.Ordinal).ToArray()),
            Alignments=rule.Alignments is null?null:Array.AsReadOnly(rule.Alignments.OrderBy(a=>a.ConnectionId,StringComparer.Ordinal).Select(a=>a with{
                Path=new WorldPolyline(Array.AsReadOnly(a.Path.Points.ToArray()))}).ToArray())};
    }

    private static InterfaceContext PrepareInterfaces(WorldSurfaceQuery surface,WorldRegionPlan? regions,WorldLocalLayoutPlan? layouts,
        IReadOnlyDictionary<string,string>? allocationBindings,IReadOnlyDictionary<string,string>? regionBindings,WorldEnvironmentDefinition? fields,
        bool needLocal,bool needRegion,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(regions is null||fields is null||needLocal&&(layouts is null||allocationBindings is null)||needRegion&&regionBindings is null)
            throw new ArgumentException("METADATA_INTERFACE_DOMAIN_DEPENDENCIES_REQUIRED");
        var environment=new WorldEnvironmentQuery(surface,fields,token);
        if(needRegion)
        {
            // The public routing API can receive a supplied region plan. Recreate
            // it from the actual surface and field owners before trusting its span.
            var expectedRegions=new WorldRegionPlanner().Plan(environment,regions.Request,budget,token);
            if(JsonSerializer.Serialize(expectedRegions,WorldJson.CreateOptions())!=JsonSerializer.Serialize(regions,WorldJson.CreateOptions()))
                throw new ArgumentException("METADATA_INTERFACE_REGION_PLAN_MISMATCH");
        }
        if(needLocal)
        {
            var expected=new WorldLocalLayoutPlanner().Plan(environment,regions,layouts!.Definition,budget,token);
            if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(layouts,WorldJson.CreateOptions()))
                throw new ArgumentException("METADATA_INTERFACE_LAYOUT_PLAN_MISMATCH");
        }
        return new(environment,regions,layouts,allocationBindings??new Dictionary<string,string>(),regionBindings??new Dictionary<string,string>());
    }

    private sealed record InterfaceDomains(string FromId,string ToId,double Minimum,IReadOnlyList<WorldRegionEdge> Spans,
        WorldRegion FromShape,WorldRegion ToShape,WorldSuitabilityProfile FromProfile,WorldSuitabilityProfile ToProfile);

    private static InterfaceDomains ResolveInterface(MetadataInterfaceBinding binding,WorldMetadataConnection link,InterfaceContext context)
    {
        string requiredFrom,requiredTo;double minimum;IReadOnlyList<WorldRegionEdge> spans;
        IReadOnlyDictionary<string,string> bindings;
        if(binding.Source==MetadataInterfaceSource.RegionRelation)
        {
            var relation=context.Regions.IntentPlan?.Relations?.SingleOrDefault(r=>r.Requirement.Id==binding.InterfaceId);
            if(relation?.SharedSpan is null||relation.SharedSegments is not {Count:>0} segments||relation.Requirement.MinimumSharedSpan is not {} width)
                throw new ArgumentException("METADATA_INTERFACE_UNKNOWN:"+binding.InterfaceId);
            requiredFrom=relation.Requirement.FromId;requiredTo=relation.Requirement.ToId;minimum=width;spans=segments;bindings=context.RegionBindings;
        }
        else
        {
            var required=context.Layouts!.Interfaces.SingleOrDefault(i=>i.Id==binding.InterfaceId)??throw new ArgumentException("METADATA_INTERFACE_UNKNOWN:"+binding.InterfaceId);
            requiredFrom=required.Requirement.FromId;requiredTo=required.Requirement.ToId;minimum=required.Requirement.MinimumSpan;spans=required.Segments;bindings=context.AllocationBindings;
        }
        if(!bindings.TryGetValue(link.FromId,out var fromId)||!bindings.TryGetValue(link.ToId,out var toId)||
            !new[]{fromId,toId}.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{requiredFrom,requiredTo}))
            throw new ArgumentException("METADATA_INTERFACE_DOMAIN_MISMATCH:"+link.Id);
        WorldRegion Shape(string id)=>binding.Source==MetadataInterfaceSource.RegionRelation?
            context.Regions.Regions.Single(r=>r.Id==id):context.Layouts!.Allocations.Single(a=>a.Id==id).Shape;
        var fromShape=Shape(fromId);var toShape=Shape(toId);
        WorldSuitabilityProfile Profile(WorldRegion shape)=>WorldRegionPlanner.ProfileFor(context.Regions,binding.Source==MetadataInterfaceSource.RegionRelation?
            shape.Id:context.Layouts!.Allocations.Single(a=>a.Id==shape.Id).RegionId);
        return new(fromId,toId,minimum,spans,fromShape,toShape,Profile(fromShape),Profile(toShape));
    }

    private static MetadataRoutedConnection RouteInterface(MetadataRouteRule rule,MetadataInterfaceBinding binding,WorldMetadataConnection link,
        WorldPoint from,WorldPoint to,WorldSurfaceQuery surface,MetadataPhysicalAccessPlan? access,WorldEnvironmentQuery environment,InterfaceDomains domains,
        IReadOnlyList<WorldRouteReusePath> existing,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var fromId=domains.FromId;var toId=domains.ToId;var fromShape=domains.FromShape;var toShape=domains.ToShape;
        var spans=domains.Spans;double minimum=domains.Minimum;
        WorldSuitabilityProfile Profile(WorldRegion shape)=>shape.Id==fromId?domains.FromProfile:domains.ToProfile;
        var areas=new[]{fromShape,toShape}.DistinctBy(s=>s.Id).ToDictionary(s=>s.Id,s=>new WorldFootprintSuitability(environment,Profile(s)),StringComparer.Ordinal);
        bool Within(WorldRegion shape,WorldPolygon polygon,WorldSpatialWorkBudget work,CancellationToken ct)
            =>WorldRegionGeometry.Contains(shape,polygon,work)&&areas[shape.Id].Matches(polygon,rule.Search.MaximumWork,4096,work,ct);
        WorldRouteConstraint Domain(WorldRegion shape)
        {
            var features=shape.BoundaryEdges.SelectMany(e=>new[]{e.From,e.To}).Distinct().ToArray();
            if(features.Length>256)throw new ArgumentException("METADATA_INTERFACE_FEATURE_BUDGET");
            return new(features,(p,work)=>Within(shape,WorldSurfaceTraversal.Envelope(p,p,rule.Profile.Radius),work,token),
                (a,b,work,ct)=>Within(shape,WorldSurfaceTraversal.Envelope(a,b,rule.Profile.Radius),work,ct));
        }
        void Entrance(string nodeId,string portId,WorldRegion shape)
        {
            var port=access?.Sites.SingleOrDefault(s=>s.NodeId==nodeId)?.Ports.SingleOrDefault(p=>p.Id==portId)
                ??throw new ArgumentException("METADATA_INTERFACE_ACCESS_REQUIRED:"+nodeId);
            ValidateInterfaceEntrance(port,nodeId,shape,environment,Profile(shape),rule.Profile,rule.Search.MaximumWork,budget,token);
        }
        Entrance(link.FromId,rule.FromPortId!,fromShape);Entrance(link.ToId,rule.ToPortId!,toShape);
        var fromDomain=Domain(fromShape);var toDomain=Domain(toShape);
        double radius=rule.Profile.Radius/Math.Cos(Math.PI/16),depth=radius+rule.Search.Clearance;
        var attempts=new List<MetadataInterfaceAttempt>();var candidates=new List<(WorldRegionEdge Span,WorldPoint Center)>();
        var qualifiedSpans=spans.OrderByDescending(s=>s.From.DistanceTo(s.To)).ThenBy(s=>s.From.X).ThenBy(s=>s.From.Y)
            .Where(s=>s.From.DistanceTo(s.To)>=Math.Max(minimum,2*radius)).ToArray();
        // Give each actual shared component a chance before retrying another
        // fraction of the same span. The declared candidate budget stays hard.
        foreach(double fraction in new[]{.5,.25,.75,.125,.875})
        {
            foreach(var span in qualifiedSpans)
            {
                double length=span.From.DistanceTo(span.To);
                if(fraction*length<radius||(1-fraction)*length<radius)continue;
                var center=new WorldPoint(span.From.X+(span.To.X-span.From.X)*fraction,span.From.Y+(span.To.Y-span.From.Y)*fraction);
                candidates.Add((span,center));if(candidates.Count==binding.MaximumCrossingCandidates)break;
            }
            if(candidates.Count==binding.MaximumCrossingCandidates)break;
        }
        foreach(var (span,center) in candidates)
        {
            long before=budget.Used;
            try
            {
                double length=span.From.DistanceTo(span.To),nx=-(span.To.Y-span.From.Y)/length,ny=(span.To.X-span.From.X)/length;
                var entry=center.Offset(nx*depth,ny*depth);var exit=center.Offset(-nx*depth,-ny*depth);
                bool In(WorldRegion r,WorldPoint p){budget.Charge(r.Cells.Sum(c=>c.Vertices.Count));return r.Cells.Any(c=>c.ContainsPoint(p));}
                if(!In(fromShape,entry)||!In(toShape,exit)){(entry,exit)=(exit,entry);nx=-nx;ny=-ny;}
                if(!In(fromShape,entry)||!In(toShape,exit))throw new MetadataSpatialConstraintException("METADATA_INTERFACE_CROSSING_OUTSIDE_DOMAINS");
                bool Crossing(WorldPoint a,WorldPoint b,WorldSpatialWorkBudget work,CancellationToken ct)
                {
                    var polygon=WorldSurfaceTraversal.Envelope(a,b,rule.Profile.Radius);
                    var left=WorldRegionGeometry.Clip(polygon.Vertices,p=>(p.X-center.X)*nx+(p.Y-center.Y)*ny,work);
                    var right=WorldRegionGeometry.Clip(polygon.Vertices,p=>-((p.X-center.X)*nx+(p.Y-center.Y)*ny),work);
                    return left.Count>=3&&right.Count>=3&&Within(fromShape,new(left),work,ct)&&Within(toShape,new(right),work,ct);
                }
                var crossingDomain=new WorldRouteConstraint([],(_,_)=>false,Crossing,true);
                var reused=existing.ToList();
                (WorldRouteResult Route,MetadataInterfaceJourney Journey) Run(bool reverse)
                {
                    string id=link.Id+(reverse?"/reverse":"/forward");
                    WorldRouteResult Leg(string suffix,WorldPoint a,WorldPoint b,WorldRouteConstraint domain)
                    {
                        if(a.DistanceTo(b)<1e-8)throw new MetadataSpatialConstraintException("METADATA_INTERFACE_ZERO_LEG");
                        var search=domain.DirectOnly?rule.Search with{Waypoints=[]}:rule.Search;
                        WorldRouteResult result;
                        try
                        {
                            result=new WorldSurfaceRoutePlanner().Plan(surface,new(id+suffix,rule.Profile.Purpose,link.SourceRuleId,a,b,rule.Profile,search,
                                rule.Reuse is {} reuse?new(reuse,reused):null),token,budget,domain);
                        }
                        catch(ArgumentException error)
                        {
                            error.Data["RouteRequestId"]=id+suffix;error.Data["InterfaceId"]=binding.InterfaceId;
                            throw;
                        }
                        if(result.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_INTERFACE_LEG_UNRESOLVED:"+suffix+":"+result.Status);
                        return result;
                    }
                    // Qualify the mandatory crossing before searching either approach leg.
                    // A blocked crossing cannot be repaired by a more expensive approach search.
                    var crossing=Leg("/crossing",reverse?exit:entry,reverse?entry:exit,crossingDomain);
                    var first=Leg("/from",reverse?to:from,reverse?exit:entry,reverse?toDomain:fromDomain);
                    var last=Leg("/to",reverse?entry:exit,reverse?from:to,reverse?fromDomain:toDomain);
                    var points=first.Path!.Points.Concat(crossing.Path!.Points.Skip(1)).Concat(last.Path!.Points.Skip(1)).ToArray();
                    if(points.Length>256)throw new ArgumentException("METADATA_INTERFACE_PATH_BUDGET");
                    var traversal=new WorldSurfaceTraversal(surface,rule.Profile,token);
                    double joinAscent=0,joinDescent=0;
                    // Each part already qualifies every internal turn. The two
                    // composed joins need their actual directed source heights
                    // and climb costs; splitting at an interface cannot erase them.
                    foreach(var join in new[]{(Before:first.Path!.Points[^2],Point:first.Path.Points[^1],After:crossing.Path!.Points[1]),
                        (Before:crossing.Path.Points[^2],Point:crossing.Path.Points[^1],After:last.Path!.Points[1])})
                    {
                        var turn=traversal.DirectedTurnCost(join.Before,join.Point,join.After,budget,token);
                        if(turn is null)throw new MetadataSpatialConstraintException("METADATA_INTERFACE_JOIN_UNTRAVERSABLE");
                        joinAscent+=turn.Value.Ascent;joinDescent+=turn.Value.Descent;
                    }
                    var parts=new[]{first,crossing,last};var cost=new WorldRouteCost(parts.Sum(p=>p.Cost!.Distance),parts.Sum(p=>p.Cost!.Terrain),
                        parts.Sum(p=>p.Cost!.Ascent)+joinAscent,parts.Sum(p=>p.Cost!.Descent)+joinDescent,parts.Sum(p=>p.Cost!.ReusedDistance),parts.Sum(p=>p.Cost!.NewPassage),parts.Sum(p=>p.Cost!.Guidance));
                    var combined=new WorldRouteResult(id,rule.Profile.Purpose,link.SourceRuleId,surface.Definition.Id,rule.Profile.Id,WorldRouteStatus.Found,
                        new(Array.AsReadOnly(points)),cost,parts.Sum(p=>p.CandidateCount),parts.Sum(p=>p.EdgeChecks),parts.Sum(p=>p.Work),"domain-interface-v1");
                    if(rule.Reuse is not null)reused.Add(new(id,combined.Path!,rule.Profile,link.Bidirectional));
                    return(combined,new(first,crossing,last,joinAscent==0&&joinDescent==0?null:new(joinAscent,joinDescent)));
                }
                var forward=Run(false);var reverse=link.Bidirectional?Run(true):((WorldRouteResult Route,MetadataInterfaceJourney Journey)?)null;
                attempts.Add(new(attempts.Count+1,center,true,null,budget.Used-before));
                return new(link.Id,rule.Id,forward.Route,reverse?.Route,new(binding.InterfaceId,fromId,toId,span,center,forward.Journey,reverse?.Journey,attempts.AsReadOnly()));
            }
            catch(MetadataSpatialConstraintException ex){attempts.Add(new(attempts.Count+1,center,false,ex.Message,budget.Used-before));}
        }
        throw new MetadataSpatialConstraintException("METADATA_INTERFACE_UNRESOLVED:"+link.Id+":"+(attempts.LastOrDefault()?.Rejection??"no-width-qualified-span"));
    }
}
