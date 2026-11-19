using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceRoutePlanner
{
    public WorldRouteResult Compile(WorldSurfaceQuery surface,WorldRouteRequest request,WorldPolyline path,CancellationToken token=default)
        =>Compile(surface,request,path,token,null);
    /// <summary>Qualifies a prescribed path without searching, snapping or replacing points.</summary>
    public WorldRouteResult Compile(WorldSurfaceDefinition definition,WorldRouteRequest request,WorldPolyline path,CancellationToken token=default)
        =>Compile(new WorldSurfaceQuery(definition,token),request,path,token,null);

    internal WorldRouteResult Compile(WorldSurfaceQuery surface,WorldRouteRequest request,WorldPolyline path,CancellationToken token,WorldSpatialWorkBudget? parent)
    {
        token.ThrowIfCancellationRequested();var search=ValidateRequest(request);
        if(search.Rounding is not null)throw new ArgumentException("WORLD_ROUTE_ROUNDING_TRACE_UNSUPPORTED");
        if(surface.Definition.Boundary is null)throw new ArgumentException("WORLD_ROUTE_BOUNDED_SURFACE_REQUIRED");
        if(request.Reuse is not null)throw new ArgumentException("WORLD_ROUTE_TRACE_REUSE_UNSUPPORTED");
        if(path?.Points is null||path.Points.Count is <2 or >1024||path.Points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
            throw new ArgumentException("WORLD_ROUTE_TRACE_INVALID");
        path=new(Array.AsReadOnly(path.Points.ToArray()));path.Validate();
        if(path.Points[0].DistanceTo(request.From)>1e-7||path.Points[^1].DistanceTo(request.To)>1e-7)throw new ArgumentException("WORLD_ROUTE_TRACE_ENDPOINT_MISMATCH");
        if(path.Points.Count-1>search.MaximumEdgeChecks)throw new ArgumentException("WORLD_ROUTE_TRACE_EDGE_BUDGET");
        var budget=new WorldSpatialWorkBudget(search.MaximumWork,token,parent);var traversal=new WorldSurfaceTraversal(surface,request.Profile,token);
        double distance=0,terrain=0,ascent=0,descent=0,guidance=0;int checks=0;double? incoming=null;
        WorldRouteResult Rejected()=>new(request.Id,request.Purpose,request.SourceRuleId,surface.Definition.Id,traversal.Profile.Id,
            WorldRouteStatus.TraceRejected,null,null,path.Points.Count,checks,budget.Used,"prescribed-polyline-v1");
        for(int i=1;i<path.Points.Count;i++)
        {
            var edge=traversal.EvaluateEdge(path.Points[i-1],path.Points[i],budget,token);
            checks++;
            if(edge is null)return Rejected();var cost=edge.Value.Cost;
            if(incoming is {} height)
            {
                if(traversal.JunctionEnvelope(path.Points[i-1],budget,token) is null)return Rejected();
                var turn=traversal.HeightTransition(height,edge.Value.FromHeight);
                if(turn is null)return Rejected();ascent+=turn.Value.Ascent;descent+=turn.Value.Descent;
            }
            incoming=edge.Value.ToHeight;
            distance+=cost.Distance;terrain+=cost.Terrain;ascent+=cost.Ascent;descent+=cost.Descent;guidance+=cost.Guidance;
        }
        return new(request.Id,request.Purpose,request.SourceRuleId,surface.Definition.Id,traversal.Profile.Id,WorldRouteStatus.Found,
            path,new(distance,terrain,ascent,descent,Guidance:guidance),path.Points.Count,checks,budget.Used,"prescribed-polyline-v1");
    }

    internal static WorldRouteSearch ValidateRequest(WorldRouteRequest request)
    {
        if(request is null||string.IsNullOrWhiteSpace(request.Id)||request.Id.Length>2048||
            string.IsNullOrWhiteSpace(request.Purpose)||request.Purpose.Length>2048||string.IsNullOrWhiteSpace(request.SourceRuleId)||request.SourceRuleId.Length>2048||
            !double.IsFinite(request.From.X)||!double.IsFinite(request.From.Y)||!double.IsFinite(request.To.X)||!double.IsFinite(request.To.Y)||
            Math.Abs(request.From.X)>1e7||Math.Abs(request.From.Y)>1e7||Math.Abs(request.To.X)>1e7||Math.Abs(request.To.Y)>1e7||
            request.From.DistanceTo(request.To)<1e-8||request.Search is not {} search||!double.IsFinite(search.Clearance)||search.Clearance<=0||search.Clearance>1e6||
            search.MaximumCandidates is <2 or >1024||search.MaximumEdgeChecks is <1 or >131072||search.MaximumWork is <1 or >200000000||!Enum.IsDefined(search.Objective)||search.Waypoints is null||search.Waypoints.Count>256)
            throw new ArgumentException("WORLD_ROUTE_REQUEST_INVALID");
        if(search.Waypoints.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))throw new ArgumentException("WORLD_ROUTE_CANDIDATE_INVALID");
        if(search.Rounding is {} rounding&&(!double.IsFinite(rounding.MaximumDeviation)||rounding.MaximumDeviation is <.000001 or >100000||rounding.SegmentsPerCorner is <2 or >16))
            throw new ArgumentException("WORLD_ROUTE_ROUNDING_INVALID");
        if(search.Rounding is {AlternateDeviations:{} alternatives} configured&&
            (alternatives.Count is <1 or >8||alternatives.Any(x=>!double.IsFinite(x)||x<.000001||x>=configured.MaximumDeviation)||
             alternatives.Zip(alternatives.Skip(1)).Any(pair=>pair.Second>=pair.First)))
            throw new ArgumentException("WORLD_ROUTE_ROUNDING_ALTERNATIVES_INVALID");
        if(search.Rounding is not null&&(search.Objective!=WorldRouteObjective.FirstFeasible||request.Reuse is not null))
            throw new ArgumentException("WORLD_ROUTE_ROUNDING_SEARCH_UNSUPPORTED");
        return search;
    }
}
