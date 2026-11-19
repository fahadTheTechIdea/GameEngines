using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceRoutePlanner
{
    internal static WorldRouteResult RoundCandidates(WorldSurfaceQuery surface,WorldRouteRequest request,WorldRouteResult original,
        WorldSpatialWorkBudget budget,WorldRouteConstraint? constraint,CancellationToken token)
    {
        var policy=request.Search.Rounding!;
        if(policy.AlternateDeviations is null)return RoundQualified(surface,request,original,budget,constraint,token);
        var attempts=new List<WorldRouteRoundingAttempt>();int totalEdges=original.EdgeChecks;
        WorldRouteResult? last=null;
        foreach(double deviation in new[]{policy.MaximumDeviation}.Concat(policy.AlternateDeviations))
        {
            token.ThrowIfCancellationRequested();long before=budget.Used;int edgesBefore=totalEdges;
            var candidate=request with{Search=request.Search with{Rounding=policy with{MaximumDeviation=deviation,AlternateDeviations=null}}};
            last=RoundQualified(surface,candidate,original with{EdgeChecks=totalEdges},budget,constraint,token);
            totalEdges=last.EdgeChecks;
            attempts.Add(new(deviation,last.Status,totalEdges-edgesBefore,budget.Used-before));
            if(last.Status==WorldRouteStatus.Found)break;
        }
        return last! with{RoundingAttempts=attempts.AsReadOnly()};
    }
    // The emitted polyline is the authoritative path. Sampling does not claim
    // continuous-curve clearance or a vehicle turning radius between samples.
    internal static WorldRouteResult RoundQualified(WorldSurfaceQuery surface,WorldRouteRequest request,WorldRouteResult original,
        WorldSpatialWorkBudget budget,WorldRouteConstraint? constraint,CancellationToken token)
    {
        var policy=request.Search.Rounding!;var source=original.Path!.Points;var points=new List<WorldPoint>{source[0]};
        bool changed=false;
        void Add(WorldPoint p)
        {
            budget.Charge();
            if(points[^1].DistanceTo(p)<1e-8)throw new ArgumentException("WORLD_ROUTE_ROUNDING_RESOLUTION");
            if(points.Count>=1024)throw new ArgumentException("WORLD_ROUTE_ROUNDING_POINT_BUDGET");
            points.Add(p);
        }
        for(int i=1;i<source.Count-1;i++)
        {
            token.ThrowIfCancellationRequested();budget.Charge(1L+request.Search.Waypoints.Count);
            var before=source[i-1];var corner=source[i];var after=source[i+1];
            double ax=before.X-corner.X,ay=before.Y-corner.Y,bx=after.X-corner.X,by=after.Y-corner.Y;
            double al=Math.Sqrt(ax*ax+ay*ay),bl=Math.Sqrt(bx*bx+by*by),cross=ax*by-ay*bx;
            // Preserve authored candidate points selected by the solver, and
            // leave collinear segments exactly as the original path.
            if(request.Search.Waypoints.Any(p=>p.DistanceTo(corner)<1e-8)||Math.Abs(cross)<=1e-12*al*bl)
            {Add(corner);continue;}
            double trim=Math.Min(policy.MaximumDeviation,.45*Math.Min(al,bl));
            var start=new WorldPoint(corner.X+ax/al*trim,corner.Y+ay/al*trim);
            var end=new WorldPoint(corner.X+bx/bl*trim,corner.Y+by/bl*trim);
            Add(start);
            for(int j=1;j<=policy.SegmentsPerCorner;j++)
            {
                double t=(double)j/policy.SegmentsPerCorner,u=1-t;
                Add(new(u*u*start.X+2*u*t*corner.X+t*t*end.X,u*u*start.Y+2*u*t*corner.Y+t*t*end.Y));
            }
            changed=true;
        }
        Add(source[^1]);
        if(!changed)return original with{Work=budget.Used};
        var traversal=new WorldSurfaceTraversal(surface,request.Profile,token);int edges=original.EdgeChecks;
        double distance=0,terrain=0,ascent=0,descent=0,guidance=0;double? incoming=null;
        var witness=new WorldRouteRoundingWitness(original.Path!,original.Cost!);
        WorldRouteResult Rejected()=>original with{Status=WorldRouteStatus.TraceRejected,Path=null,Cost=null,EdgeChecks=edges,Work=budget.Used,
            SearchModel=original.SearchModel+"/rounded-polyline-v1",RoundingWitness=witness};
        for(int i=1;i<points.Count;i++)
        {
            token.ThrowIfCancellationRequested();
            if(++edges>request.Search.MaximumEdgeChecks)throw new ArgumentException("WORLD_ROUTE_EDGE_BUDGET");
            if(i<points.Count-1&&constraint is not null&&!constraint.AllowsCandidate(points[i],budget))return Rejected();
            var edge=traversal.EvaluateEdge(points[i-1],points[i],budget,token);
            if(edge is null||constraint is not null&&!constraint.AllowsSegment(points[i-1],points[i],budget,token))return Rejected();
            var cost=edge.Value.Cost;
            if(incoming is {} height)
            {
                if(traversal.JunctionEnvelope(points[i-1],budget,token) is null)return Rejected();
                var turn=traversal.HeightTransition(height,edge.Value.FromHeight);
                if(turn is null)return Rejected();ascent+=turn.Value.Ascent;descent+=turn.Value.Descent;
            }
            incoming=edge.Value.ToHeight;
            distance+=cost.Distance;terrain+=cost.Terrain;ascent+=cost.Ascent;descent+=cost.Descent;guidance+=cost.Guidance;
        }
        return original with{Path=new(Array.AsReadOnly(points.ToArray())),Cost=new(distance,terrain,ascent,descent,Guidance:guidance),
            EdgeChecks=edges,Work=budget.Used,SearchModel=original.SearchModel+"/rounded-polyline-v1",RoundingWitness=witness};
    }
}
