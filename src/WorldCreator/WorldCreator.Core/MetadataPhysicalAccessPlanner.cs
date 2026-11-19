using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataOccupancyKind { Unspecified, Reservation, Solid }
public sealed record MetadataAccessPort(string Id,string Purpose,string SourceRuleId,
    WorldPoint BoundaryPosition,WorldPoint ApproachPosition,double Width);
public sealed record MetadataCatalogAccess(MetadataOccupancyKind Occupancy,IReadOnlyList<MetadataAccessPort> Ports);
public sealed record MetadataPhysicalSite(string NodeId,MetadataOccupancyKind Occupancy,string SourceRuleId,
    WorldPolygon Footprint,IReadOnlyList<MetadataAccessPort> Ports);
public sealed record MetadataPhysicalAccessPlan(IReadOnlyDictionary<string,MetadataCatalogAccess> Definitions,
    IReadOnlyList<MetadataPhysicalSite> Sites,long Work);
internal sealed record MetadataPhysicalAllocation(string NodeId,double RotationDegrees,WorldPoint Position,WorldPolygon Footprint,string SourceRuleId,WorldPolygon? Reservation=null);
/// <summary>A valid declaration cannot be realized at the current candidate positions.</summary>
internal sealed class MetadataSpatialConstraintException(string diagnostic,Exception? inner=null):ArgumentException(diagnostic,inner);
internal sealed class MetadataPlacementInfeasibleException(string diagnostic,Exception? inner=null):ArgumentException(diagnostic,inner);

/// <summary>Owns source access declarations, physical occupancy and boundary approaches.
/// Routes use the common terrain solver, with solid footprints as ordinary exclusions.</summary>
public sealed class MetadataPhysicalAccessPlanner
{
    public const int MaximumSites=1024;
    public MetadataPhysicalAccessPlan Plan(IReadOnlyDictionary<string,MetadataCatalogAccess> definitions,
        IReadOnlyList<WorldMetadataNode> nodes,MetadataPlacementPlan? placement,WorldSurfaceDefinition surface,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(placement is null)throw new ArgumentException("METADATA_ACCESS_PLACEMENT_REQUIRED");
        return Plan(definitions,nodes,Allocations(placement),surface,structures,null,token);
    }

    internal static IReadOnlyList<MetadataPhysicalAllocation> Allocations(MetadataPlacementPlan placement)
    {
        var stages=new[]{(placement.Definition.Bindings,placement.Placement,placement.Footprints)}.Concat(
            placement.NestedStages.Select(s=>(placement.Definition.NestedStages!.Single(d=>d.Id==s.Id).Bindings,s.Placement,s.Footprints)));
        var allocations=stages.SelectMany(stage=>stage.Bindings.Select(binding=>{
            var site=stage.Placement.Sites.Single(s=>s.DemandId==binding.DemandId);
            return new MetadataPhysicalAllocation(binding.NodeId,binding.RotationDegrees,site.Position,site.Footprint.Footprint,
                stage.Footprints.Single(s=>s.NodeId==binding.NodeId).SourceRuleId,site.Reservation?.Footprint);
        })).ToArray();
        return allocations;
    }

    internal MetadataPhysicalAccessPlan Plan(IReadOnlyDictionary<string,MetadataCatalogAccess> definitions,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<MetadataPhysicalAllocation> allocations,WorldSurfaceDefinition surface,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,WorldSpatialWorkBudget? sharedBudget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(allocations.Count>MaximumSites)throw new ArgumentException("METADATA_ACCESS_SITE_BUDGET");
        var budget=new WorldSpatialWorkBudget(20000000,token,sharedBudget);
        if(definitions is null||definitions.Count is <1 or >256)throw new ArgumentException("METADATA_ACCESS_PLACEMENT_REQUIRED");
        var snapshots=new SortedDictionary<string,MetadataCatalogAccess>(StringComparer.Ordinal);
        foreach(var (id,definition) in definitions.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(!WorldEnvironmentQuery.Id(id)||definition is null||definition.Occupancy is not (MetadataOccupancyKind.Reservation or MetadataOccupancyKind.Solid)||
                definition.Ports is null||definition.Ports.Count>16)throw new ArgumentException("METADATA_ACCESS_DEFINITION_INVALID");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in definition.Ports)
                if(p is null||!WorldEnvironmentQuery.Id(p.Id)||!ids.Add(p.Id)||!WorldEnvironmentQuery.Text(p.Purpose)||!WorldEnvironmentQuery.Text(p.SourceRuleId)||
                    !Point(p.BoundaryPosition)||!Point(p.ApproachPosition)||!double.IsFinite(p.Width)||p.Width<=0||p.Width>1e6)
                    throw new ArgumentException("METADATA_ACCESS_PORT_INVALID");
            snapshots.Add(id,definition with{Ports=Array.AsReadOnly(definition.Ports.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray())});
        }
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var sites=new List<MetadataPhysicalSite>();
        foreach(var allocated in allocations)
        {
            token.ThrowIfCancellationRequested();
            var node=lookup[allocated.NodeId];
            if(!snapshots.TryGetValue(node.ArchetypeId,out var source))throw new ArgumentException("METADATA_ACCESS_CLASSIFICATION_REQUIRED:"+node.Id);
            budget.Charge((long)allocated.Footprint.Vertices.Count*allocated.Footprint.Vertices.Count*(source.Ports.Count+1));
            var positioned=PositionSite(allocated,source);
            foreach(var port in positioned.Ports)
            {
                if(node.StructureKey is {} key&&structures.TryGetValue(key,out var structure)&&port.Width+1e-8<structure.Definition.AccessWidthMeters)
                    throw new ArgumentException("METADATA_ACCESS_CATALOG_WIDTH:"+node.Id+":"+port.Id);
            }
            sites.Add(positioned);
        }
        var placed=sites.Select(s=>s.NodeId).ToHashSet(StringComparer.Ordinal);
        if(nodes.Any(n=>snapshots.ContainsKey(n.ArchetypeId)&&!placed.Contains(n.Id))||snapshots.Keys.Any(k=>!nodes.Any(n=>n.ArchetypeId==k)))
            throw new ArgumentException("METADATA_ACCESS_UNALLOCATED_DECLARATION");
        if(sites.Sum(s=>s.Ports.Count)>128)throw new ArgumentException("METADATA_ACCESS_PORT_BUDGET");
        var result=new MetadataPhysicalAccessPlan(new ReadOnlyDictionary<string,MetadataCatalogAccess>(snapshots),
            Array.AsReadOnly(sites.OrderBy(s=>s.NodeId,StringComparer.Ordinal).ToArray()),0);
        ValidateSolidSet(surface,result,token);
        // All declared openings reserve their full width, even before a route selects them.
        var baseQuery=new WorldSurfaceQuery(surface,token);
        long baseEdges=(surface.Boundary?.Vertices.Count??0)+surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count);
        foreach(var site in result.Sites.Where(s=>s.Ports.Count>0))
        {
            foreach(var port in site.Ports)
            {
                token.ThrowIfCancellationRequested();
                // Base domain/exclusion qualification and each solid intersection are
                // conjunctive. Remote solids need only the shared bounding-box witness,
                // not a fictitious quadratic interaction with every entrance.
                budget.Charge(4*(baseEdges+1)*(baseEdges+1));
                if(!baseQuery.ContainsCorridor(new([port.BoundaryPosition,port.ApproachPosition]),port.Width/2,token)||
                    result.Sites.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid&&s.NodeId!=site.NodeId)
                        .Any(s=>WorldSurfaceQuery.IntersectsExcludedPolygon(s.Footprint,port.BoundaryPosition,port.ApproachPosition,port.Width/2,budget,token)))
                    throw new MetadataSpatialConstraintException("METADATA_ACCESS_APPROACH_BLOCKED:"+site.NodeId+":"+port.Id);
            }
        }
        return result with{Work=budget.Used};
    }

    private static void ValidateOpening(WorldPolygon shape,MetadataAccessPort port)
    {
        var p=port.BoundaryPosition;var q=port.ApproachPosition;double length=p.DistanceTo(q);
        if(length<port.Width*.51||shape.ContainsPoint(q))throw new ArgumentException("METADATA_ACCESS_DIRECTION_INVALID:"+port.Id);
        double dx=(q.X-p.X)/length,dy=(q.Y-p.Y)/length;
        for(int i=0;i<shape.Vertices.Count;i++)
        {
            var a=shape.Vertices[i];var b=shape.Vertices[(i+1)%shape.Vertices.Count];double edge=a.DistanceTo(b);
            double ex=(b.X-a.X)/edge,ey=(b.Y-a.Y)/edge,along=(p.X-a.X)*ex+(p.Y-a.Y)*ey;
            if(Math.Abs((p.X-a.X)*ey-(p.Y-a.Y)*ex)>1e-7||along<port.Width/2-1e-7||along>edge-port.Width/2+1e-7||Math.Abs(dx*ex+dy*ey)>1e-7)continue;
            // The approach must leave this edge outward, not cross a concave interior elsewhere.
            var strip=ApproachBoundary(port);
            if(shape.ContainsPoint(strip.Centroid())||strip.Vertices.Where((v,j)=>shape.CrossesInterior(v,strip.Vertices[(j+1)%strip.Vertices.Count])).Any()||
                shape.Vertices.Where((v,j)=>strip.CrossesInterior(v,shape.Vertices[(j+1)%shape.Vertices.Count])).Any())
                throw new ArgumentException("METADATA_ACCESS_DIRECTION_INVALID:"+port.Id);
            return;
        }
        throw new ArgumentException("METADATA_ACCESS_BOUNDARY_OPENING_INVALID:"+port.Id);
    }

    internal static MetadataPhysicalSite PositionSite(MetadataPhysicalAllocation allocated,MetadataCatalogAccess source)
    {
        var shape=MetadataFootprintResolver.Polygon(allocated.Footprint);
        double angle=allocated.RotationDegrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
        WorldPoint Transform(WorldPoint p)=>new(p.X*c-p.Y*s+allocated.Position.X,p.X*s+p.Y*c+allocated.Position.Y);
        var ports=source.Ports.Select(p=>p with{BoundaryPosition=Transform(p.BoundaryPosition),ApproachPosition=Transform(p.ApproachPosition)}).ToArray();
        foreach(var port in ports)ValidateOpening(shape,port);
        return new(allocated.NodeId,source.Occupancy,allocated.SourceRuleId,shape,Array.AsReadOnly(ports));
    }

    /// <summary>Exact butt-ended entrance strip, in the port's world/source frame.</summary>
    public static WorldPolygon ApproachBoundary(MetadataAccessPort port)
    {
        ArgumentNullException.ThrowIfNull(port);var p=port.BoundaryPosition;var q=port.ApproachPosition;
        double length=p.DistanceTo(q);
        if(!double.IsFinite(length)||length<1e-8||!double.IsFinite(port.Width)||port.Width<=0)
            throw new ArgumentException("METADATA_ACCESS_APPROACH_GEOMETRY_INVALID");
        double sx=-(q.Y-p.Y)/length*port.Width/2,sy=(q.X-p.X)/length*port.Width/2;
        var shape=new WorldPolygon([p.Offset(sx,sy),q.Offset(sx,sy),q.Offset(-sx,-sy),p.Offset(-sx,-sy)]);
        shape.Validate();return shape;
    }

    public WorldSurfaceQuery RouteSurface(WorldSurfaceDefinition surface,MetadataPhysicalAccessPlan? plan,CancellationToken token=default,string? approachOwner=null)
    {
        ValidateSolidSet(surface,plan,token);
        var exclusions=new List<WorldSurfaceExclusion>();
        foreach(var site in plan?.Sites??[])
        {
            token.ThrowIfCancellationRequested();if(site.Occupancy!=MetadataOccupancyKind.Solid||site.NodeId==approachOwner)continue;
            string id="physical-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(site.NodeId))).ToLowerInvariant();
            exclusions.Add(new(id,"Solid occupancy: "+site.NodeId,site.SourceRuleId,site.Footprint));
        }
        return new(surface,token,exclusions.AsReadOnly());
    }

    internal static void ValidateSolidSet(WorldSurfaceDefinition surface,MetadataPhysicalAccessPlan? plan,CancellationToken token)
    {
        if(plan is null)return;
        if(plan.Sites is null||plan.Sites.Count>MaximumSites)throw new ArgumentException("METADATA_ACCESS_SITE_BUDGET");
        int vertices=(surface.Boundary?.Vertices.Count??0)+surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+surface.Patches.Sum(p=>p.Boundary.Vertices.Count);
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var site in plan.Sites)
        {
            token.ThrowIfCancellationRequested();
            if(site is null||!WorldEnvironmentQuery.Text(site.NodeId)||!ids.Add(site.NodeId)||!WorldEnvironmentQuery.Text(site.SourceRuleId)||
                site.Occupancy is not (MetadataOccupancyKind.Solid or MetadataOccupancyKind.Reservation))throw new ArgumentException("METADATA_ACCESS_SITE_INVALID");
            if(site.Occupancy!=MetadataOccupancyKind.Solid)continue;
            if(site.Footprint?.Vertices is not {Count:>=3 and <=1024}||(vertices+=site.Footprint.Vertices.Count)>16384||site.Footprint.Vertices.Any(p=>!Point(p)))
                throw new ArgumentException("WORLD_SURFACE_GEOMETRY_BUDGET_OR_INPUT_INVALID");
            site.Footprint.Validate();
        }
    }
    internal static bool IntersectsSolids(MetadataPhysicalAccessPlan? plan,WorldPoint from,WorldPoint to,double radius,
        WorldSpatialWorkBudget budget,CancellationToken token,string? owner=null)
        =>(plan?.Sites??[]).Any(s=>s.Occupancy==MetadataOccupancyKind.Solid&&s.NodeId!=owner&&
            WorldSurfaceQuery.IntersectsExcludedPolygon(s.Footprint,from,to,radius,budget,token));

    public WorldPoint Endpoint(WorldMetadataNode node,string? portId,MetadataPhysicalAccessPlan? plan,
        WorldSurfaceDefinition surface,WorldTraversalProfile profile,CancellationToken token=default)
    {
        ValidateSolidSet(surface,plan,token);
        return Endpoint(node,portId,plan,surface,profile,new WorldSpatialWorkBudget(20000000,token),token);
    }

    internal WorldPoint Endpoint(WorldMetadataNode node,string? portId,MetadataPhysicalAccessPlan? plan,
        WorldSurfaceDefinition surface,WorldTraversalProfile profile,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var site=plan?.Sites.SingleOrDefault(s=>s.NodeId==node.Id);
        if(portId is null)
        {
            if(site?.Occupancy==MetadataOccupancyKind.Solid)throw new ArgumentException("METADATA_ACCESS_PORT_REQUIRED:"+node.Id);
            return node.Position is {Count:2} p?new(p[0],p[1]):throw new ArgumentException("METADATA_ROUTE_ENDPOINT_REQUIRED:"+node.Id);
        }
        var port=site?.Ports.SingleOrDefault(p=>p.Id==portId)??throw new ArgumentException("METADATA_ACCESS_PORT_MISSING:"+node.Id+":"+portId);
        if(profile.Radius*2>port.Width+1e-8)throw new ArgumentException("METADATA_ACCESS_ROUTE_TOO_WIDE:"+node.Id+":"+portId);
        var traversal=new WorldSurfaceTraversal(new WorldSurfaceQuery(surface,token),profile,token);
        // Qualify both directions; this is a boundary interface, not an exempt external route.
        if(traversal.Evaluate(port.BoundaryPosition,port.ApproachPosition,budget,token) is null||traversal.Evaluate(port.ApproachPosition,port.BoundaryPosition,budget,token) is null||
            IntersectsSolids(plan,port.BoundaryPosition,port.ApproachPosition,profile.Radius,budget,token,node.Id))
            throw new MetadataSpatialConstraintException("METADATA_ACCESS_APPROACH_UNTRAVERSABLE:"+node.Id+":"+portId);
        return port.ApproachPosition;
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        if(document.AccessPlan is not {} plan)return;
        if(document.Surface is null)throw new ArgumentException("METADATA_ACCESS_SURFACE_REQUIRED");
        var expected=Plan(plan.Definitions,document.Nodes,document.PlacementPlan,document.Surface,document.StructureDefinitions,token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_ACCESS_PLAN_MISMATCH");
    }
    private static bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=1e7&&Math.Abs(p.Y)<=1e7;
}
