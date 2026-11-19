using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceRoutePlanner
{
    private sealed record ReuseEdge(WorldPoint A,WorldPoint B,bool Bidirectional);
    private sealed record ReuseContext(double Rate,IReadOnlyList<ReuseEdge> Edges,IReadOnlyList<WorldPoint> Vertices);
    internal static void ValidateReuse(WorldRouteReuseSettings? settings)
    {
        if(settings is not null&&(!double.IsFinite(settings.NewPassageCostPerMeter)||settings.NewPassageCostPerMeter<0||
            settings.NewPassageCostPerMeter>1000000||settings.MaximumSourceEdges is <1 or >1024))
            throw new ArgumentException("WORLD_ROUTE_REUSE_SETTINGS_INVALID");
    }
    private static ReuseContext PrepareReuse(WorldRouteReuse? reuse,WorldSurfaceQuery surface,WorldSurfaceTraversal traversal,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(reuse is null)return new(0,[],[]);
        if(reuse.Settings is null||reuse.Paths is null||reuse.Paths.Count>128)throw new ArgumentException("WORLD_ROUTE_REUSE_INVALID");
        ValidateReuse(reuse.Settings);var ids=new HashSet<string>(StringComparer.Ordinal);var edges=new List<ReuseEdge>();
        foreach(var source in reuse.Paths.OrderBy(p=>p?.Id,StringComparer.Ordinal))
        {
            budget.Charge();
            if(source is null||string.IsNullOrWhiteSpace(source.Id)||source.Id.Length>2048||!ids.Add(source.Id)||source.Path?.Points is not {Count:>=2 and <=256})
                throw new ArgumentException("WORLD_ROUTE_REUSE_SOURCE_INVALID");
            var profile=new WorldSurfaceTraversal(surface,source.Profile,token).Profile;
            if(WorldSurfaceTraversal.CompatibilityKey(profile)!=WorldSurfaceTraversal.CompatibilityKey(traversal.Profile))
                throw new ArgumentException("WORLD_ROUTE_REUSE_PROFILE_INCOMPATIBLE");
            var points=source.Path.Points.ToArray();new WorldPolyline(points).Validate();
            if(points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
                throw new ArgumentException("WORLD_ROUTE_REUSE_POINT_INVALID");
            for(int i=1;i<points.Length;i++)
            {
                if(edges.Count>=reuse.Settings.MaximumSourceEdges)throw new ArgumentException("WORLD_ROUTE_REUSE_EDGE_BUDGET");
                if(traversal.Evaluate(points[i-1],points[i],budget,token) is null||source.Bidirectional&&traversal.Evaluate(points[i],points[i-1],budget,token) is null)
                    throw new ArgumentException("WORLD_ROUTE_REUSE_SOURCE_UNRESOLVED:"+source.Id);
                edges.Add(new(points[i-1],points[i],source.Bidirectional));
            }
        }
        return new(reuse.Settings.NewPassageCostPerMeter,edges.AsReadOnly(),edges.SelectMany(e=>new[]{e.A,e.B}).Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray());
    }
    private static WorldPoint Project(WorldPoint p,WorldPoint a,WorldPoint b)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
        if(t==0)return a;if(t==1)return b;
        return new(a.X+t*dx,a.Y+t*dy);
    }
    private static WorldRouteCost ReuseCost(WorldRouteCost cost,WorldPoint a,WorldPoint b,ReuseContext reuse,WorldSpatialWorkBudget budget)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,length=a.DistanceTo(b);var intervals=new List<(double Start,double End)>();
        foreach(var source in reuse.Edges)
        {
            budget.Charge();
            double sx=source.B.X-source.A.X,sy=source.B.Y-source.A.Y;
            if(!source.Bidirectional&&dx*sx+dy*sy<=0)continue;
            double Side(WorldPoint p)=>Math.Abs(dx*(p.Y-a.Y)-dy*(p.X-a.X))/length;
            if(Side(source.A)>1e-8||Side(source.B)>1e-8)continue;
            double Along(WorldPoint p)=>((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(length*length);
            double p=Along(source.A),q=Along(source.B),start=Math.Max(0,Math.Min(p,q)),end=Math.Min(1,Math.Max(p,q));
            if(end>start)intervals.Add((start,end));
        }
        double covered=0,last=0;
        foreach(var interval in intervals.OrderBy(i=>i.Start).ThenBy(i=>i.End))
        {covered+=Math.Max(0,interval.End-Math.Max(last,interval.Start));last=Math.Max(last,interval.End);}
        double distance=Math.Clamp(covered*length,0,cost.Distance);
        return cost with{ReusedDistance=distance,NewPassage=(cost.Distance-distance)*reuse.Rate};
    }
}
