using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceRoutePlanner
{
    // Construct finite search proposals from declared anchors and local axes.
    // These points are never world objects or a sampling lattice.
    private static void GuidanceFeatures(WorldRouteRequest request,WorldSurfaceTraversal traversal,
        WorldSpatialWorkBudget budget,Action<WorldPoint,int> candidate)
    {
        foreach(var rule in (traversal.Profile.Guidance??[]).OrderBy(r=>r.Id,StringComparer.Ordinal))
        {
            var axes=WorldSurfaceTraversal.GuidanceAxes(rule);
            var anchors=new[]{request.From,request.To}.Concat(request.Search.Waypoints).Concat(rule.Boundary.Vertices)
                .Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
            bool Inside(WorldPoint p){budget.Charge(rule.Boundary.Vertices.Count);return double.IsFinite(p.X)&&double.IsFinite(p.Y)&&rule.Boundary.ContainsPoint(p);}
            foreach(var p in rule.Boundary.Vertices)candidate(p,0);
            foreach(var a in anchors)
            foreach(var axis in axes)
            {
                for(int i=0;i<rule.Boundary.Vertices.Count;i++)
                {
                    budget.Charge();var c=rule.Boundary.Vertices[i];var d=rule.Boundary.Vertices[(i+1)%rule.Boundary.Vertices.Count];
                    double ex=d.X-c.X,ey=d.Y-c.Y,denominator=axis.X*ey-axis.Y*ex;
                    if(Math.Abs(denominator)<1e-10)continue;
                    double u=((c.X-a.X)*axis.Y-(c.Y-a.Y)*axis.X)/denominator;
                    if(u< -1e-10||u>1+1e-10)continue;
                    var point=new WorldPoint(c.X+Math.Clamp(u,0,1)*ex,c.Y+Math.Clamp(u,0,1)*ey);
                    candidate(point,0);
                }
                foreach(var b in anchors)
                {
                    budget.Charge();
                    if(axes.Count==1)
                    {
                        double along=((b.X-a.X)*axis.X+(b.Y-a.Y)*axis.Y)/(axis.X*axis.X+axis.Y*axis.Y);
                        var projected=a.Offset(along*axis.X,along*axis.Y);if(Inside(projected))candidate(projected,0);
                    }
                    foreach(var other in axes)
                    {
                        budget.Charge();double denominator=axis.X*other.Y-axis.Y*other.X;
                        if(Math.Abs(denominator)<1e-8)continue;
                        double t=((b.X-a.X)*other.Y-(b.Y-a.Y)*other.X)/denominator;
                        var point=a.Offset(t*axis.X,t*axis.Y);if(Inside(point))candidate(point,0);
                    }
                }
            }
        }
    }
}
