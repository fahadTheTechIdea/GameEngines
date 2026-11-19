using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataBoundarySource { Footprint, Reservation, Region, Allocation }
/// <summary>References accepted source geometry. Null node selectors mean the actual program parent;
/// PartId selects one of that parent's declared parts. Region/allocation loops require explicit choice when ambiguous.</summary>
public sealed record MetadataGeneratedBoundaryPattern(MetadataBoundarySource Source,double Spacing,
    WorldBoundarySide Side=WorldBoundarySide.Inside,double Offset=0,double StartDistance=0,int StartEdgeIndex=0,double? SpanLength=null,
    string? NodeId=null,string? PartId=null,int PartIndex=0,string? RegionId=null,string? AllocationId=null,int? LoopIndex=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool UseLongestEdge=false);
public sealed record MetadataGeneratedBoundaryResult(string? SourceNodeId,string? RegionId,string? AllocationId,string SourceGeometrySha256,
    int LoopIndex,int LoopCount,bool Hole,WorldBoundaryPattern Pattern,IReadOnlyList<WorldPoint> Positions);
public sealed record MetadataPopulationBoundaryTrace(string StageId,string DemandId,string Purpose,string SourceRuleId,
    IReadOnlyList<string> NodeIds,MetadataGeneratedBoundaryResult Boundary);

internal static class MetadataGeneratedBoundary
{
    internal static MetadataGeneratedBoundaryResult Sample(MetadataGeneratedBoundaryPattern pattern,int count,WorldMetadataDocument? source,
        string? parent,WorldSpatialWorkBudget budget,CancellationToken token,Action<IReadOnlyList<WorldPopulationPose>,bool>? recordPoses=null)
    {
        token.ThrowIfCancellationRequested();
        if(pattern is null||!Enum.IsDefined(pattern.Source)||!Enum.IsDefined(pattern.Side)||pattern.PartIndex<0||pattern.LoopIndex is <0||
            pattern.NodeId is {} selectedId&&(!WorldEnvironmentQuery.Text(selectedId)||selectedId.Length>2048)||
            !double.IsFinite(pattern.Spacing)||pattern.Spacing<=0||!double.IsFinite(pattern.Offset)||pattern.Offset<0||
            !double.IsFinite(pattern.StartDistance)||pattern.StartDistance<0||pattern.StartEdgeIndex<0||
            pattern.SpanLength is {} span&&(!double.IsFinite(span)||span<0)||pattern.UseLongestEdge&&pattern.StartEdgeIndex!=0)
            throw new ArgumentException("METADATA_GENERATED_BOUNDARY_INVALID");
        if(source?.Surface is null||source.Space.Axes.Count!=2)throw new ArgumentException("METADATA_GENERATED_BOUNDARY_SOURCE_REQUIRED");
        WorldRegion? region=null;WorldPolygon? polygon=null;string? nodeId=null;IReadOnlyList<WorldRegionEdge>? edges=null;
        if(pattern.Source is MetadataBoundarySource.Footprint or MetadataBoundarySource.Reservation)
        {
            if(pattern.RegionId is not null||pattern.AllocationId is not null||pattern.NodeId is not null&&pattern.PartId is not null||
                pattern.PartId is null&&pattern.PartIndex!=0)throw new ArgumentException("METADATA_GENERATED_BOUNDARY_SELECTOR_INVALID");
            string owner=parent??source.Id;budget.Charge(source.Nodes.Count);
            if(pattern.PartId is {} part)
            {
                if(!WorldEnvironmentQuery.Id(part))throw new ArgumentException("METADATA_GENERATED_BOUNDARY_SELECTOR_INVALID");
                nodeId=source.Nodes.SingleOrDefault(n=>MetadataCapacityPlanner.IsPartChild(n,owner,part)&&
                    n.Id==owner+"/"+part+"-"+pattern.PartIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))?.Id
                    ??throw new ArgumentException("METADATA_GENERATED_BOUNDARY_PART_MISSING");
            }
            else nodeId=pattern.NodeId??owner;
            if(pattern.Source==MetadataBoundarySource.Footprint)
            {
                budget.Charge(source.AccessPlan?.Sites.Count??0);
                polygon=source.AccessPlan?.Sites.SingleOrDefault(s=>s.NodeId==nodeId)?.Footprint
                    ??throw new ArgumentException("METADATA_GENERATED_BOUNDARY_FOOTPRINT_MISSING");
            }
            else
            {
                if(source.PlacementPlan is null)throw new ArgumentException("METADATA_GENERATED_BOUNDARY_RESERVATION_MISSING");
                budget.Charge(source.AccessPlan?.Sites.Count??0);
                polygon=MetadataPhysicalAccessPlanner.Allocations(source.PlacementPlan).SingleOrDefault(s=>s.NodeId==nodeId)?.Reservation
                    ??throw new ArgumentException("METADATA_GENERATED_BOUNDARY_RESERVATION_MISSING");
            }
        }
        else
        {
            if(pattern.NodeId is not null||pattern.PartId is not null||pattern.PartIndex!=0||!WorldEnvironmentQuery.Id(pattern.RegionId)||
                pattern.Source==MetadataBoundarySource.Region&&pattern.AllocationId is not null||
                pattern.Source==MetadataBoundarySource.Allocation&&!WorldEnvironmentQuery.Id(pattern.AllocationId))
                throw new ArgumentException("METADATA_GENERATED_BOUNDARY_SELECTOR_INVALID");
            budget.Charge(source.RegionPlan?.Regions.Count??0);
            region=source.RegionPlan?.Regions.SingleOrDefault(r=>r.Id==pattern.RegionId)
                ??throw new ArgumentException("METADATA_GENERATED_BOUNDARY_REGION_MISSING");
            if(pattern.Source==MetadataBoundarySource.Allocation)
            {
                budget.Charge(source.LocalLayoutPlan?.Allocations.Count??0);
                var allocation=source.LocalLayoutPlan?.Allocations.SingleOrDefault(a=>a.Id==pattern.AllocationId&&a.RegionId==pattern.RegionId)
                    ??throw new ArgumentException("METADATA_GENERATED_BOUNDARY_ALLOCATION_MISSING");
                region=allocation.Shape;
            }
            edges=region.BoundaryEdges;
        }
        string hash=MetadataExecutionCache.Hash(new{source.Space,pattern.Source,NodeId=nodeId,pattern.RegionId,pattern.AllocationId,Polygon=polygon,Edges=edges});
        IReadOnlyList<WorldPolygon> loops;
        try{loops=polygon is not null?[Canonical(polygon.Vertices,budget)]:Loops(edges!,budget,token);}
        catch(ArgumentException error)when(error.Message=="METADATA_GENERATED_BOUNDARY_TOPOLOGY_UNRESOLVED")
        {error.Data["BoundaryCells"]=region?.Cells;throw;}
        if(pattern.LoopIndex is null&&loops.Count!=1)throw new ArgumentException("METADATA_GENERATED_BOUNDARY_LOOP_REQUIRED");
        int selected=pattern.LoopIndex??0;
        if(selected>=loops.Count)throw new ArgumentException("METADATA_GENERATED_BOUNDARY_LOOP_MISSING");
        var boundary=loops[selected];bool hole=false;
        for(int i=0;i<loops.Count;i++)if(i!=selected)
        {budget.Charge(loops[i].Vertices.Count);if(loops[i].ContainsPoint(boundary.Vertices[0]))hole=!hole;}
        var side=hole?(pattern.Side==WorldBoundarySide.Inside?WorldBoundarySide.Outside:WorldBoundarySide.Inside):pattern.Side;
        int startEdge=pattern.StartEdgeIndex;
        if(pattern.UseLongestEdge)
        {
            double length=-1;
            for(int i=0;i<boundary.Vertices.Count;i++)
            {
                budget.Charge();double candidate=boundary.Vertices[i].DistanceTo(boundary.Vertices[(i+1)%boundary.Vertices.Count]);
                if(candidate>length){length=candidate;startEdge=i;}
            }
        }
        var resolved=new WorldBoundaryPattern(boundary,startEdge,pattern.Spacing,pattern.StartDistance,pattern.Offset,pattern.SpanLength,side);
        budget.Charge((long)boundary.Vertices.Count*boundary.Vertices.Count+Math.Max(0,count)*(long)boundary.Vertices.Count);
        var poses=WorldPopulationGeometry.BoundaryPoses(resolved,count,token);
        recordPoses?.Invoke(poses,hole);
        var positions=Array.AsReadOnly(poses.Select(p=>p.Position).ToArray());
        if(region is not null)
        foreach(var point in positions)
        {
            budget.Charge(region.Cells.Sum(c=>c.Vertices.Count));bool inside=region.Cells.Any(c=>c.ContainsPoint(point));
            if(pattern.Side==WorldBoundarySide.Inside&&!inside||pattern.Side==WorldBoundarySide.Outside&&pattern.Offset>0&&inside)
                throw new ArgumentException("METADATA_GENERATED_BOUNDARY_REGION_SIDE_INVALID");
        }
        return new(nodeId,pattern.RegionId,pattern.AllocationId,hash,selected,loops.Count,hole,resolved,positions);
    }

    internal static IReadOnlyList<WorldPolygon> Loops(IReadOnlyList<WorldRegionEdge> edges,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(edges is not {Count:>=3 and <=32768})throw new ArgumentException("METADATA_GENERATED_BOUNDARY_EDGES_INVALID");
        var adjacency=new Dictionary<WorldPoint,HashSet<WorldPoint>>();
        foreach(var e in edges)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(!double.IsFinite(e.From.X)||!double.IsFinite(e.From.Y)||!double.IsFinite(e.To.X)||!double.IsFinite(e.To.Y)||e.From.DistanceTo(e.To)<=1e-9)
                throw new ArgumentException("METADATA_GENERATED_BOUNDARY_EDGES_INVALID");
            if(!adjacency.TryGetValue(e.From,out var from))adjacency.Add(e.From,from=[]);
            if(!adjacency.TryGetValue(e.To,out var to))adjacency.Add(e.To,to=[]);
            if(!from.Add(e.To)||!to.Add(e.From))throw new ArgumentException("METADATA_GENERATED_BOUNDARY_DUPLICATE_EDGE");
        }
        if(adjacency.Values.Any(v=>v.Count!=2))
        {
            var error=new ArgumentException("METADATA_GENERATED_BOUNDARY_TOPOLOGY_UNRESOLVED");
            error.Data["BoundaryEdges"]=edges;throw error;
        }
        var remaining=adjacency.Keys.ToHashSet();var loops=new List<WorldPolygon>();
        foreach(var first in adjacency.Keys.OrderBy(p=>p.X).ThenBy(p=>p.Y))
        {
            if(!remaining.Contains(first))continue;
            var points=new List<WorldPoint>();WorldPoint? previous=null;var current=first;
            do
            {
                token.ThrowIfCancellationRequested();budget.Charge();
                if(!remaining.Remove(current))throw new ArgumentException("METADATA_GENERATED_BOUNDARY_TOPOLOGY_UNRESOLVED");
                points.Add(current);
                var next=adjacency[current].Where(p=>previous is null||p!=previous).OrderBy(p=>p.X).ThenBy(p=>p.Y).First();
                previous=current;current=next;
            }while(current!=first);
            loops.Add(Canonical(points,budget));
        }
        return loops.OrderBy(p=>p.Vertices[0].X).ThenBy(p=>p.Vertices[0].Y).ThenBy(p=>Math.Abs(p.SignedArea())).ToArray();
    }
    private static WorldPolygon Canonical(IReadOnlyList<WorldPoint> original,WorldSpatialWorkBudget budget)
    {
        var points=new List<WorldPoint>();
        bool Straight(WorldPoint a,WorldPoint b,WorldPoint c)
        {budget.Charge();return WorldRegionExact.Orientation(a,b,c)==0&&(b.X-a.X)*(b.X-c.X)+(b.Y-a.Y)*(b.Y-c.Y)<=0;}
        foreach(var p in original)
        {
            while(points.Count>=2&&Straight(points[^2],points[^1],p))points.RemoveAt(points.Count-1);points.Add(p);
        }
        bool changed=true;
        while(changed&&points.Count>3)
        {
            changed=false;
            if(Straight(points[^1],points[0],points[1])){points.RemoveAt(0);changed=true;}
            if(points.Count>3&&Straight(points[^2],points[^1],points[0])){points.RemoveAt(points.Count-1);changed=true;}
        }
        if(points.Count is <3 or >256)throw new ArgumentException("METADATA_GENERATED_BOUNDARY_VERTEX_LIMIT");
        budget.Charge((long)points.Count*points.Count);var polygon=new WorldPolygon(points.ToArray());polygon.Validate();
        if(polygon.SignedArea()<0)points.Reverse();
        int start=Enumerable.Range(0,points.Count).OrderBy(i=>points[i].X).ThenBy(i=>points[i].Y).First();
        return new WorldPolygon(Array.AsReadOnly(Enumerable.Range(0,points.Count).Select(i=>points[(start+i)%points.Count]).ToArray()));
    }
}
