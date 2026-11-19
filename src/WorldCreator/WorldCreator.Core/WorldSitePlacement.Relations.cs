using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSitePlacementPlanner
{
    // This bounds arithmetic rounding, not a gameplay allowance. Coordinate ULPs
    // matter after translating a small layout far from zero. Exact zero pins
    // remain exact, including source-world preservation constraints.
    private static double Ulp(double value)=>Math.BitIncrement(Math.Abs(value))-Math.Abs(value);
    private static bool RelationDistanceAccepts(WorldPoint first,WorldPoint second,double minimum,double maximum)
    {
        if(minimum==0&&maximum==0)return first==second;
        double distance=first.DistanceTo(second);
        double error=4*(Ulp(first.X)+Ulp(first.Y)+Ulp(second.X)+Ulp(second.Y)+Ulp(distance)+Ulp(minimum)+Ulp(maximum));
        // An allowance larger than a positive bound must not make an unresolved
        // tiny radius accept coincident or visibly different representable poses.
        return (distance>=minimum||minimum>error&&distance>=minimum-error)&&
            (distance<=maximum||maximum>error&&distance<=maximum+error);
    }

    private sealed record RelationCircle(string Id,WorldPoint Center,double Radius,string Role);

    private static void RelationCandidates(WorldPlacementRequest request,IReadOnlyDictionary<string,WorldPoint> anchors,
        IReadOnlyDictionary<WorldPoint,string> features,IReadOnlyDictionary<(WorldPoint From,WorldPoint To),string> edges,
        WorldSpatialWorkBudget budget,Action<WorldPoint,string> candidate,string? targetDemandId=null)
    {
        var boundary=request.Boundary!;budget.Charge(boundary.Vertices.Count*4L+request.Relations.Count);
        double minX=boundary.Vertices.Min(p=>p.X),maxX=boundary.Vertices.Max(p=>p.X),minY=boundary.Vertices.Min(p=>p.Y),maxY=boundary.Vertices.Max(p=>p.Y);
        var orderedFeatures=features.OrderBy(p=>p.Key.X).ThenBy(p=>p.Key.Y).ToArray();
        var orderedEdges=edges.OrderBy(p=>p.Key.From.X).ThenBy(p=>p.Key.From.Y).ThenBy(p=>p.Key.To.X).ThenBy(p=>p.Key.To.Y).ToArray();
        void Emit(WorldPoint point,string source)
        {
            budget.Charge();
            if(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||Math.Abs(point.X)>1e7||Math.Abs(point.Y)>1e7)return;
            double error=4*(Ulp(point.X)+Ulp(point.Y)+Ulp(minX)+Ulp(maxX)+Ulp(minY)+Ulp(maxY));
            if(point.X<minX-error||point.X>maxX+error||point.Y<minY-error||point.Y>maxY+error)return;
            candidate(point,source);
        }
        var applicable=targetDemandId is null?request.Relations.Where(r=>anchors.ContainsKey(r.ToId)):
            request.Relations.Where(r=>r.FromId==targetDemandId&&anchors.ContainsKey(r.ToId)||r.ToId==targetDemandId&&anchors.ContainsKey(r.FromId))
                .Select(r=>r.FromId==targetDemandId?r:r with{FromId=targetDemandId,ToId=r.FromId});
        foreach(var group in applicable.GroupBy(r=>r.FromId).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            var circles=new List<RelationCircle>();
            foreach(var relation in group)
            {
                var radii=new Dictionary<double,string>();
                radii.TryAdd(relation.Ideal,"ideal");radii.TryAdd(relation.Minimum,"minimum");radii.TryAdd(relation.Maximum,"maximum");
                foreach(var radius in radii)
                {
                    budget.Charge();
                    if(radius.Key==0)
                    {
                        if(targetDemandId is not null)Emit(anchors[relation.ToId],"relation:"+relation.Id+":"+radius.Value+":coincident");
                        continue;
                    }
                    circles.Add(new(relation.Id,anchors[relation.ToId],radius.Key,radius.Value));
                }
            }
            for(int i=0;i<circles.Count;i++)
            {
                var circle=circles[i];string source="relation:"+circle.Id+":"+circle.Role;
                foreach(var feature in orderedFeatures)
                {
                    budget.Charge(8);double dx=feature.Key.X-circle.Center.X,dy=feature.Key.Y-circle.Center.Y,length=Math.Sqrt(dx*dx+dy*dy);
                    if(length==0)continue;
                    Emit(circle.Center.Offset(circle.Radius*dx/length,circle.Radius*dy/length),source+":direction:"+feature.Value);
                }
                foreach(var anchor in anchors)
                {
                    budget.Charge(8);double dx=anchor.Value.X-circle.Center.X,dy=anchor.Value.Y-circle.Center.Y,length=Math.Sqrt(dx*dx+dy*dy);
                    if(length==0)continue;
                    Emit(circle.Center.Offset(circle.Radius*dx/length,circle.Radius*dy/length),source+":toward:"+anchor.Key);
                }
                foreach(var edge in orderedEdges)
                {
                    budget.Charge(24);var a=edge.Key.From;var b=edge.Key.To;
                    double dx=b.X-a.X,dy=b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);if(length==0)continue;
                    double ux=dx/length,uy=dy/length,cx=circle.Center.X-a.X,cy=circle.Center.Y-a.Y;
                    double along=cx*ux+cy*uy,normal=cx*uy-cy*ux;
                    double h2=(circle.Radius-Math.Abs(normal))*(circle.Radius+Math.Abs(normal));
                    double error=8*(Ulp(circle.Radius)+Ulp(normal)+Ulp(cx)+Ulp(cy));
                    if(h2< -error*Math.Max(1,circle.Radius))continue;
                    double h=Math.Sqrt(Math.Max(0,h2));
                    foreach(double at in new[]{along-h,along+h})
                    {
                        budget.Charge();if(at<0||at>length)continue;
                        Emit(a.Offset(ux*at,uy*at),source+":boundary:"+edge.Value);
                    }
                }
                for(int j=0;j<i;j++)
                {
                    budget.Charge(24);var other=circles[j];
                    double dx=other.Center.X-circle.Center.X,dy=other.Center.Y-circle.Center.Y,distance=Math.Sqrt(dx*dx+dy*dy);
                    if(distance==0)continue; // Concentric constraints have no isolated intersection.
                    double error=8*(Ulp(circle.Radius)+Ulp(other.Radius)+Ulp(distance));
                    if(distance>circle.Radius+other.Radius+error||distance<Math.Abs(circle.Radius-other.Radius)-error)continue;
                    double along=((circle.Radius-other.Radius)*(circle.Radius+other.Radius)+distance*distance)/(2*distance);
                    double h2=(circle.Radius-along)*(circle.Radius+along);
                    if(h2< -error*Math.Max(1,circle.Radius))continue;
                    double h=Math.Sqrt(Math.Max(0,h2)),ux=dx/distance,uy=dy/distance;
                    var center=circle.Center.Offset(ux*along,uy*along);
                    Emit(center.Offset(-uy*h,ux*h),source+":intersection:"+other.Id+":"+other.Role);
                    Emit(center.Offset(uy*h,-ux*h),source+":intersection:"+other.Id+":"+other.Role);
                }
            }
        }
    }
}
