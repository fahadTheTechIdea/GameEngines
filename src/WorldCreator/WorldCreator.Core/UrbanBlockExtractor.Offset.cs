using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Geometry;

public static partial class UrbanBlockExtractor
{
    private static WorldPolygon Inset(WorldPolygon boundary,IReadOnlyList<double> widths,CancellationToken token)
    {
        var points=boundary.Vertices; var inset=new WorldPoint[points.Count];
        var starts=new WorldPoint[points.Count]; var directions=new WorldPoint[points.Count];
        for(int i=0;i<points.Count;i++)
        {
            var a=points[i];var b=points[(i+1)%points.Count];double length=a.DistanceTo(b);
            directions[i]=new((b.X-a.X)/length,(b.Y-a.Y)/length);
            starts[i]=a.Offset(-directions[i].Y*widths[i],directions[i].X*widths[i]);
        }
        for(int i=0;i<points.Count;i++)
        {
            token.ThrowIfCancellationRequested();
            int previous=(i+points.Count-1)%points.Count;
            var a=starts[previous];var b=starts[i];var u=directions[previous];var v=directions[i];
            double cross=u.X*v.Y-u.Y*v.X;
            if(Math.Abs(cross)<1e-10)
            {
                if(u.X*v.X+u.Y*v.Y<0 || Math.Abs(widths[i]-widths[previous])>Tolerance)
                    throw new ArgumentException("BLOCK_OFFSET_STRAIGHT_PROFILE_TRANSITION_UNSUPPORTED");
                inset[i]=b;continue;
            }
            double distance=((b.X-a.X)*v.Y-(b.Y-a.Y)*v.X)/cross;
            inset[i]=a.Offset(distance*u.X,distance*u.Y);
        }
        var result=new WorldPolygon(inset);result.Validate();
        double area=result.SignedArea();
        if(area<=Tolerance || area>=boundary.SignedArea())throw new ArgumentException("NO_BUILDABLE_INTERIOR");
        for(int i=0;i<points.Count;i++)
        {
            var u=directions[i];var a=inset[i];var b=inset[(i+1)%points.Count];
            if((b.X-a.X)*u.X+(b.Y-a.Y)*u.Y<=Tolerance)
                throw new ArgumentException("BLOCK_OFFSET_TOPOLOGY_CHANGE_UNSUPPORTED");
        }
        var triangles=LandscapePolygonTriangulator.Triangulate(boundary);
        double covered=0;
        foreach(var triangle in triangles)
        {
            token.ThrowIfCancellationRequested();
            covered+=TerrainCoverageTopology.IntersectionArea(result,triangle);
        }
        if(Math.Abs(covered-area)>Math.Max(1e-6,area*1e-8))throw new ArgumentException("BLOCK_OFFSET_OUTSIDE_FACE");
        return result;
    }
}
