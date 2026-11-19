using System.Collections.ObjectModel;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

internal readonly record struct WorldTraversalEdge(WorldRouteCost Cost,double FromHeight,double ToHeight);

/// <summary>One owner for game-authored passage feasibility and terrain costs. Geometry is in the
/// surface unit. The circular envelope allows sharp bends; it is not a vehicle turning model.</summary>
public sealed partial class WorldSurfaceTraversal
{
    private readonly WorldSurfaceQuery surface;
    private readonly WorldSurfaceAreaQuery areas;
    private readonly int edgeCount;
    private readonly (double MinX,double MinY,double MaxX,double MaxY)[] edgeBounds;
    private readonly (double X,double Y)? uniformGradient;
    private readonly WorldTerrainPatch? uniformElevationSource;
    public WorldTraversalProfile Profile { get; }
    internal double MinimumCost => Profile.KindCosts.Values.Min();
    private sealed record Compatibility(double Radius,double MaximumSlope,IReadOnlyDictionary<string,double> KindCosts,
        double AscentCost,double DescentCost,double MaximumHeightStep,
        [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRouteGuidance>? Guidance);
    internal static string CompatibilityKey(WorldTraversalProfile p)=>System.Text.Json.JsonSerializer.Serialize(
        new Compatibility(p.Radius,p.MaximumSlope,p.KindCosts,p.AscentCost,p.DescentCost,p.MaximumHeightStep,p.Guidance),WorldJson.CreateOptions());

    public WorldSurfaceTraversal(WorldSurfaceQuery surface, WorldTraversalProfile profile,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        this.surface=surface??throw new ArgumentNullException(nameof(surface));
        if(profile is null || !SettlementCatalogRegistry.ValidId(profile.Id) ||
            string.IsNullOrWhiteSpace(profile.Purpose) || profile.Purpose.Length>2048 ||
            string.IsNullOrWhiteSpace(profile.SourceRuleId) || profile.SourceRuleId.Length>2048 ||
            !Number(profile.Radius) || profile.Radius<=0 || !Number(profile.MaximumSlope) ||
            !Number(profile.AscentCost) || !Number(profile.DescentCost) || !Number(profile.MaximumHeightStep) ||
            profile.KindCosts is null || profile.KindCosts.Count is <1 or >256 ||
            profile.KindCosts.Any(k=>!SettlementCatalogRegistry.ValidId(k.Key)||!Number(k.Value)||k.Value<=0))
            throw new ArgumentException("WORLD_TRAVERSAL_PROFILE_INVALID");
        Profile=profile with{KindCosts=new ReadOnlyDictionary<string,double>(new SortedDictionary<string,double>(
            profile.KindCosts.ToDictionary(k=>k.Key,k=>k.Value),StringComparer.Ordinal)),Guidance=FreezeGuidance(profile.Guidance,surface,token)};
        areas=new(surface,false);
        var d=surface.Definition;
        edgeCount=(d.Boundary?.Vertices.Count??0)+d.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+d.Patches.Sum(p=>p.Boundary.Vertices.Count);
        edgeBounds=(d.Boundary is {} boundary?new[]{boundary}:[]).Concat(d.Exclusions.Select(e=>e.Boundary)).Concat(d.Patches.Select(p=>p.Boundary))
            .SelectMany(p=>p.Vertices.Select((a,i)=>(A:a,B:p.Vertices[(i+1)%p.Vertices.Count])))
            .Select(e=>(Math.Min(e.A.X,e.B.X),Math.Min(e.A.Y,e.B.Y),Math.Max(e.A.X,e.B.X),Math.Max(e.A.Y,e.B.Y))).ToArray();
        var allowed=d.Patches.Where(p=>Profile.KindCosts.ContainsKey(p.Kind)).ToArray();
        if(surface.HeightField is null&&allowed.Length>0)
        {
            var first=allowed[0];double intercept=first.Elevation-first.Origin.X*first.GradientX-first.Origin.Y*first.GradientY;
            if(allowed.All(p=>p.GradientX==first.GradientX&&p.GradientY==first.GradientY&&
                p.Elevation-p.Origin.X*p.GradientX-p.Origin.Y*p.GradientY==intercept))uniformGradient=(first.GradientX,first.GradientY);
            if(allowed.All(p=>p.GradientX==first.GradientX&&p.GradientY==first.GradientY&&p.Origin==first.Origin&&p.Elevation==first.Elevation))uniformElevationSource=first;
        }
    }

    internal double CostLowerBound(WorldPoint from,WorldPoint to,WorldSpatialWorkBudget? budget=null)
    {
        budget?.Charge(xCostSlabs.Count+yCostSlabs.Count);
        double bound=from.DistanceTo(to)*MinimumCost+CostBandLowerBound(from,to);
        // On a shared affine plane every feasible path has at least this net
        // ascent/descent, even when obstacles require additional bends.
        if(uniformGradient is {} gradient)
        {
            double delta=(to.X-from.X)*gradient.X+(to.Y-from.Y)*gradient.Y;
            bound+=Math.Max(0,delta)*Profile.AscentCost+Math.Max(0,-delta)*Profile.DescentCost;
        }
        return bound;
    }

    public WorldRouteCost? Evaluate(WorldPoint from, WorldPoint to, CancellationToken token=default)
        => Evaluate(from,to,new WorldSpatialWorkBudget(20000000,token),token);

    internal bool Allows(WorldSurfaceSample s)=>s.Status==WorldSurfaceStatus.Sampled &&
        s.Kind is {} kind && Profile.KindCosts.ContainsKey(kind) && s.Slope<=Profile.MaximumSlope;

    internal WorldRouteCost? Evaluate(WorldPoint from, WorldPoint to, WorldSpatialWorkBudget budget,CancellationToken token)
        =>EvaluateEdge(from,to,budget,token)?.Cost;

    internal double? UniformElevation(WorldPoint point)=>uniformElevationSource is {} p?
        p.Elevation+(point.X-p.Origin.X)*p.GradientX+(point.Y-p.Origin.Y)*p.GradientY:null;

    internal WorldTraversalEdge? EvaluateEdge(WorldPoint from,WorldPoint to,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var line=new WorldPolyline([from,to]);
        if(surface.IntersectsExclusion(from,to,Profile.Radius,budget,token))return null;
        // Trace scans every boundary, but only edges intersecting the segment's
        // bounding box can add sampling intervals. Charge the actual upper bound
        // n * k instead of assuming every remote structure intersects every route.
        int intersecting=ChargeTraceWork(from,to,budget);
        // A rejected centerline cannot become a valid swept passage. Qualify it first
        // so disconnected interiors do not spend envelope work on known-invalid edges.
        if (surface.HeightField is {} field) budget.Charge(WorldHeightFieldQuery.CutWork(field.Geometry,from,to));
        var spans=surface.TraceTerrain(from,to,token);
        double distance=0,terrain=0,ascent=0,descent=0;double? previous=null,firstHeight=null;
        foreach(var span in spans)
        {
            budget.Charge();
            var s=span.Sample;
            if(!Allows(s))return null;
            var startHeight=surface.ElevationOnSource(s,span.From,token);var endHeight=surface.ElevationOnSource(s,span.To,token);
            if(startHeight is null||endHeight is null)return null;
            double start=startHeight.Value,end=endHeight.Value,length=span.From.DistanceTo(span.To);
            firstHeight??=start;
            if(previous is {} last&&!JunctionHeightsCompatible(last,start))return null;
            double delta=end-start,step=previous is {} h?start-h:0;
            distance+=length;terrain+=length*Profile.KindCosts[s.Kind!];
            ascent+=(Math.Max(0,delta)+Math.Max(0,step))*Profile.AscentCost;
            descent+=(Math.Max(0,-delta)+Math.Max(0,-step))*Profile.DescentCost;
            previous=end;
        }
        budget.Charge(4L*(edgeCount+1)*(intersecting+1));
        if(!surface.ContainsTerrainCorridor(line,Profile.Radius,token))return null;
        if(!AllowsEnvelope(Envelope(from,to,Profile.Radius),budget,token))return null;
        return firstHeight is {} first&&previous is {} finalHeight?
            new(new(distance,terrain,ascent,descent,Guidance:GuidanceCost(from,to,budget,token)),first,finalHeight):null;
    }

    internal WorldPolygon? Junction(WorldPoint point,IReadOnlyList<WorldPoint> approaches,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var envelope=JunctionEnvelope(point,budget,token);if(envelope is null)return null;
        var heights=new List<double>();
        foreach(var approach in approaches)
        {
            var height=ApproachElevation(point,approach,budget,token);if(height is null)return null;
            heights.Add(height.Value);
        }
        return heights.Count>0&&JunctionHeightsCompatible(heights.Min(),heights.Max())?envelope:null;
    }

    internal WorldPolygon? JunctionEnvelope(WorldPoint point,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(surface.IntersectsExclusion(point,point,Profile.Radius,budget,token))return null;
        var envelope=Envelope(point,point,Profile.Radius);
        if(!AllowsEnvelope(envelope,budget,token))return null;
        return envelope;
    }

    internal double? ApproachElevation(WorldPoint point,WorldPoint approach,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        ChargeTraceWork(point,approach,budget);
        if(surface.HeightField is {} field)budget.Charge(WorldHeightFieldQuery.CutWork(field.Geometry,point,approach));
        var first=surface.TraceTerrain(point,approach,token).FirstOrDefault();
        if(first is null||!Allows(first.Sample))return null;
        return surface.ElevationOnSource(first.Sample,point,token);
    }

    private bool JunctionHeightsCompatible(double a,double b)=>double.IsFinite(a)&&double.IsFinite(b)&&Math.Abs(a-b)<=Profile.MaximumHeightStep+1e-8;

    internal (double Ascent,double Descent)? HeightTransition(double from,double to)
        =>JunctionHeightsCompatible(from,to)?(Math.Max(0,to-from)*Profile.AscentCost,Math.Max(0,from-to)*Profile.DescentCost):null;

    internal (double Ascent,double Descent)? DirectedTurnCost(WorldPoint before,WorldPoint point,WorldPoint after,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(JunctionEnvelope(point,budget,token) is null)return null;
        ChargeTraceWork(before,point,budget);
        if(surface.HeightField is {} field)budget.Charge(WorldHeightFieldQuery.CutWork(field.Geometry,before,point));
        var previous=surface.TraceTerrain(before,point,token).LastOrDefault();
        if(previous is null||!Allows(previous.Sample))return null;
        var incoming=surface.ElevationOnSource(previous.Sample,point,token);var outgoing=ApproachElevation(point,after,budget,token);
        return incoming is {} a&&outgoing is {} b?HeightTransition(a,b):null;
    }

    // TraceCore scans all edges, then samples at the cuts contributed by
    // intersecting edges. Junctions and ordinary segments use the same bound;
    // remote walls cannot add cuts to a local approach segment.
    private int ChargeTraceWork(WorldPoint from,WorldPoint to,WorldSpatialWorkBudget budget)
    {
        budget.Charge(edgeCount);
        double minX=Math.Min(from.X,to.X),maxX=Math.Max(from.X,to.X),minY=Math.Min(from.Y,to.Y),maxY=Math.Max(from.Y,to.Y);
        int intersecting=edgeBounds.Count(e=>e.MinX<=maxX+1e-8&&e.MaxX>=minX-1e-8&&e.MinY<=maxY+1e-8&&e.MaxY>=minY-1e-8);
        budget.Charge(2L*(edgeCount+1)*(intersecting+1));
        return intersecting;
    }

    // Convex hull of two circumscribed 16-gons contains the whole circular swept segment.
    // At most sec(pi/16)-1 (~1.96%) extra radial clearance is conservatively reserved.
    internal static WorldPolygon Envelope(WorldPoint from,WorldPoint to,double radius)
    {
        double r=radius/Math.Cos(Math.PI/16);
        // Construct opposite rays from the same magnitudes. Independent trig
        // evaluations made nominal axis/symmetric vertices differ by roundoff,
        // introducing artificial near-zero arrangement faces downstream.
        double c=Math.Cos(Math.PI/8),s=Math.Sin(Math.PI/8),d=Math.Sqrt(.5);
        (double X,double Y)[] directions=[(1,0),(c,s),(d,d),(s,c),(0,1),(-s,c),(-d,d),(-c,s),
            (-1,0),(-c,-s),(-d,-d),(-s,-c),(0,-1),(s,-c),(d,-d),(c,-s)];
        var points=new[]{from,to}.SelectMany(p=>directions.Select(v=>p.Offset(r*v.X,r*v.Y)))
            .Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
        static double Cross(WorldPoint a,WorldPoint b,WorldPoint c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        var lower=new List<WorldPoint>();var upper=new List<WorldPoint>();
        foreach(var p in points)
        {
            while(lower.Count>=2 && Cross(lower[^2],lower[^1],p)<=0)lower.RemoveAt(lower.Count-1);
            lower.Add(p);
        }
        foreach(var p in points.Reverse())
        {
            while(upper.Count>=2 && Cross(upper[^2],upper[^1],p)<=0)upper.RemoveAt(upper.Count-1);
            upper.Add(p);
        }
        return new(lower.SkipLast(1).Concat(upper.SkipLast(1)).ToArray());
    }
    private static bool Number(double v)=>double.IsFinite(v)&&v>=0&&v<=1000000;
}
