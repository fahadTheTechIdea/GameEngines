using WorldCreator.Core.Geometry;
namespace WorldCreator.Core.Geometry;

/// <summary>Robust-enough convex polygon clipping for planned parcels; concave polygons are explicitly unsupported.</summary>
public static class ConvexParcelGeometry
{
    private const double Eps = 1e-8;
    /// <summary>Smallest distance between parallel supporting lines of a convex polygon.</summary>
    public static double MinimumWidth(WorldPolygon polygon)
    {
        if(!IsConvex(polygon))throw new NotSupportedException("Only convex polygons are supported.");
        double width=double.PositiveInfinity;
        for(int i=0;i<polygon.Vertices.Count;i++)
        {
            var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Count];double length=a.DistanceTo(b);
            width=Math.Min(width,polygon.Vertices.Max(p=>Math.Abs((b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X))/length));
        }
        return width;
    }
    /// <summary>Shortest geometric side; collinear topology terminals do not create artificial short sides.</summary>
    public static double MinimumSideLength(WorldPolygon polygon)
    {
        if(!IsConvex(polygon))throw new NotSupportedException("Only convex polygons are supported.");
        var points=polygon.Vertices;
        var corners=Enumerable.Range(0,points.Count).Where(i=>{
            var a=points[(i+points.Count-1)%points.Count];var b=points[i];var c=points[(i+1)%points.Count];
            double ux=b.X-a.X,uy=b.Y-a.Y,vx=c.X-b.X,vy=c.Y-b.Y;
            return Math.Abs(ux*vy-uy*vx)>1e-10*Math.Sqrt((ux*ux+uy*uy)*(vx*vx+vy*vy));
        }).Select(i=>points[i]).ToArray();
        return Enumerable.Range(0,corners.Length).Min(i=>corners[i].DistanceTo(corners[(i+1)%corners.Length]));
    }
    /// <summary>Clips a convex polygon to nx*x+ny*y >= distance. Empty intersections return null.</summary>
    public static WorldPolygon? ClipHalfPlane(WorldPolygon polygon,double nx,double ny,double distance)
    {
        if(!new[]{nx,ny,distance}.All(double.IsFinite) || Math.Abs(nx)+Math.Abs(ny)<Eps)throw new ArgumentException("Invalid clipping half-plane");
        if(!IsConvex(polygon))throw new NotSupportedException("Only convex polygons are supported.");
        var output=new List<WorldPoint>();var points=polygon.Vertices;
        for(int i=0;i<points.Count;i++)
        {
            var a=points[i];var b=points[(i+1)%points.Count];double da=nx*a.X+ny*a.Y-distance,db=nx*b.X+ny*b.Y-distance;
            if((da>=-Eps)!=(db>=-Eps))
            {
                double t=da/(da-db);output.Add(new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y)));
            }
            if(db>=-Eps)output.Add(b);
        }
        var clean=new List<WorldPoint>();foreach(var p in output)if(clean.Count==0||clean[^1].DistanceTo(p)>Eps)clean.Add(p);
        if(clean.Count>1&&clean[0].DistanceTo(clean[^1])<=Eps)clean.RemoveAt(clean.Count-1);
        if(clean.Count<3 || Math.Abs(StreetSurfaceBuilder.SignedArea(clean))<=Eps)return null;
        var result=new WorldPolygon(clean);result.Validate();return result;
    }
    private static double Cross(WorldPoint a, WorldPoint b, WorldPoint c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    public static bool IsConvex(WorldPolygon polygon)
    {
        polygon.Validate();
        double sign = 0;
        for (int i = 0; i < polygon.Vertices.Count; i++)
        {
            double turn = Cross(polygon.Vertices[i], polygon.Vertices[(i + 1) % polygon.Vertices.Count],
                polygon.Vertices[(i + 2) % polygon.Vertices.Count]);
            if (Math.Abs(turn) <= Eps) continue;
            if (sign != 0 && turn * sign < 0) return false;
            sign = Math.Sign(turn);
        }
        return true;
    }
    /// <summary>Splits a convex polygon by vertical line x=coordinate; halves are non-overlapping.</summary>
    public static (WorldPolygon Left, WorldPolygon Right) SplitVertical(WorldPolygon polygon, double coordinate)
    {
        if (!double.IsFinite(coordinate)) throw new ArgumentOutOfRangeException(nameof(coordinate));
        if (!IsConvex(polygon)) throw new NotSupportedException("Only convex polygons are supported.");
        var min = polygon.Vertices.Min(v => v.X);
        var max = polygon.Vertices.Max(v => v.X);
        if (coordinate <= min + Eps || coordinate >= max - Eps)
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Split must lie inside polygon bounds.");
        var left = Clip(polygon.Vertices, coordinate, true);
        var right = Clip(polygon.Vertices, coordinate, false);
        return (new WorldPolygon(left), new WorldPolygon(right));
    }
    private static IReadOnlyList<WorldPoint> Clip(IReadOnlyList<WorldPoint> input, double x, bool keepLeft)
    {
        var output = new List<WorldPoint>();
        bool Inside(WorldPoint p) => keepLeft ? p.X <= x + Eps : p.X >= x - Eps;
        for (int i = 0; i < input.Count; i++)
        {
            var a = input[i]; var b = input[(i + 1) % input.Count];
            bool ia = Inside(a), ib = Inside(b);
            if (ia && ib) output.Add(b);
            else if (ia != ib)
            {
                double t = (x - a.X) / (b.X - a.X);
                var crossing = new WorldPoint(x, a.Y + (b.Y - a.Y) * t);
                output.Add(crossing);
                if (ib) output.Add(b);
            }
        }
        var clean = new List<WorldPoint>();
        foreach (var p in output)
            if (clean.Count == 0 || clean[^1].DistanceTo(p) > Eps) clean.Add(p);
        if (clean.Count > 1 && clean[0].DistanceTo(clean[^1]) <= Eps) clean.RemoveAt(clean.Count - 1);
        return clean;
    }
}
