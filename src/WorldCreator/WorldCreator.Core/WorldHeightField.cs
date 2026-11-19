using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

/// <summary>Node samples, row-major, with a lower-left to upper-right diagonal in each cell.
/// The sampling lattice describes terrain only, never a world layout.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldHeightField(int SchemaVersion, string Id, string Kind, string Purpose,
    string SourceRuleId, string Unit, WorldPoint Origin, double StepX, double StepY,
    int Columns, int Rows, IReadOnlyList<double> Heights);

/// <summary>Constant-time planar triangle sampling; immutable field snapshot.</summary>
public sealed class WorldHeightFieldQuery : IWorldHeightFieldQuery
{
    public WorldHeightField Definition { get; }
    public WorldHeightFieldGeometry Geometry {get;}
    public WorldHeightField CompleteField=>Definition;
    public double MaxX => Geometry.MaxX;
    public double MaxY => Geometry.MaxY;

    public WorldHeightFieldQuery(WorldHeightField field, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ValidateHeader(field);
        if(field.Heights is null || field.Heights.Count != (long)field.Columns * field.Rows)
            throw new ArgumentException("WORLD_HEIGHTFIELD_INVALID");
        var values = new double[field.Heights.Count];
        for (int i = 0; i < values.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            if (!Number(field.Heights[i])) throw new ArgumentException("WORLD_HEIGHTFIELD_HEIGHT_INVALID");
            values[i] = field.Heights[i];
        }
        Definition = field with { Heights = Array.AsReadOnly(values) };
        Geometry=Describe(Definition);
    }

    public static WorldHeightFieldGeometry Describe(WorldHeightField field)
    {
        ValidateHeader(field);
        return new(field.SchemaVersion,field.Id,field.Kind,field.Purpose,field.SourceRuleId,field.Unit,
            field.Origin,field.StepX,field.StepY,field.Columns,field.Rows);
    }

    public double HeightAt(int column,int row)
    {
        if(column<0||row<0||column>=Geometry.Columns||row>=Geometry.Rows)
            throw new ArgumentException("WORLD_HEIGHTFIELD_SAMPLE_INDEX_INVALID");
        return Definition.Heights[row*Geometry.Columns+column];
    }
    public WorldScalarSample SampleCell(int column,int row,double u,double v)=>CellCore(this,column,row,u,v);
    internal static WorldScalarSample CellCore(IWorldHeightFieldQuery query,int column,int row,double u,double v)
    {
        var f=query.Geometry;
        if(column<0||row<0||column>=f.Columns-1||row>=f.Rows-1||!double.IsFinite(u)||!double.IsFinite(v)||u< -1e-8||u>1+1e-8||v< -1e-8||v>1+1e-8)
            throw new ArgumentException("WORLD_HEIGHTFIELD_CELL_INVALID");
        return WorldTriangularInterpolation.Sample(query.HeightAt(column,row),query.HeightAt(column+1,row),
            query.HeightAt(column,row+1),query.HeightAt(column+1,row+1),u,v,f.StepX,f.StepY);
    }

    // Terrain generation validates provenance and global geometry without allocating
    // a complete sample buffer merely to validate a small independent window.
    internal static void ValidateHeader(WorldHeightField field)
    {
        if (field is null || field.SchemaVersion != 1 || !Id(field.Id) || !Id(field.Kind) || !Id(field.Unit) ||
            !Text(field.Purpose) || !Text(field.SourceRuleId) || !Point(field.Origin) ||
            !double.IsFinite(field.StepX) || field.StepX < .001 || !double.IsFinite(field.StepY) || field.StepY < .001 ||
            field.Columns is < 2 or > 513 || field.Rows is < 2 or > 513 ||
            !Number(field.Origin.X + (field.Columns - 1) * field.StepX) ||
            !Number(field.Origin.Y + (field.Rows - 1) * field.StepY))
            throw new ArgumentException("WORLD_HEIGHTFIELD_INVALID");
    }

    public WorldSurfaceSample Sample(WorldPoint point, CancellationToken token = default)
        =>SampleCore(this,point,token);
    internal static WorldSurfaceSample SampleCore(IWorldHeightFieldQuery query,WorldPoint point,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Point(point)) throw new ArgumentException("WORLD_SURFACE_POINT_INVALID");
        var f = query.Geometry;
        if (point.X < f.Origin.X || point.Y < f.Origin.Y || point.X > f.MaxX || point.Y > f.MaxY)
            return new(point, WorldSurfaceStatus.Uncovered);
        double x = (point.X - f.Origin.X) / f.StepX, y = (point.Y - f.Origin.Y) / f.StepY;
        int col = Math.Min((int)Math.Floor(x), f.Columns - 2), row = Math.Min((int)Math.Floor(y), f.Rows - 2);
        double u = x - col, v = y - row;
        var value=query.SampleCell(col,row,u,v);
        return new(point, WorldSurfaceStatus.Sampled, f.Id, f.SourceRuleId, f.Kind,
            value.Value,value.GradientX,value.GradientY);
    }

    public IEnumerable<double> Cuts(WorldPoint a, WorldPoint b, CancellationToken token)=>CutsCore(Geometry,a,b,token);
    internal static (int First,int Last) CutRange(double origin,double step,int count,double a,double b)
    {
        double low=Math.Min(a,b),high=Math.Max(a,b),end=origin+(count-1)*step;
        if(high<origin||low>end)return (1,0);
        // Retain neighboring indices around each computed quotient. Original
        // coordinates and the original strict t predicate still decide cuts;
        // quotient rounding cannot omit a source boundary.
        int first=(int)Math.Clamp(Math.Floor((low-origin)/step)-1,0,count-1);
        int last=(int)Math.Clamp(Math.Ceiling((high-origin)/step)+1,0,count-1);
        return(first,last);
    }
    internal static long CutWork(WorldHeightFieldGeometry f,WorldPoint a,WorldPoint b)
    {
        var columns=a.X==b.X?(First:1,Last:0):CutRange(f.Origin.X,f.StepX,f.Columns,a.X,b.X);
        var rows=a.Y==b.Y?(First:1,Last:0):CutRange(f.Origin.Y,f.StepY,f.Rows,a.Y,b.Y);
        return 8+4L*(columns.Last-columns.First+1+rows.Last-rows.First+1);
    }
    internal static IEnumerable<double> CutsCore(WorldHeightFieldGeometry f,WorldPoint a,WorldPoint b,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!Point(a)||!Point(b))throw new ArgumentException("WORLD_SURFACE_POINT_INVALID");
        var cuts = new SortedSet<double> { 0, 1 };
        double dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx != 0)
        {
            var range=CutRange(f.Origin.X,f.StepX,f.Columns,a.X,b.X);
            for(int c=range.First;c<=range.Last;c++)Add((f.Origin.X+c*f.StepX-a.X)/dx);
        }
        if (dy != 0)
        {
            var range=CutRange(f.Origin.Y,f.StepY,f.Rows,a.Y,b.Y);
            for(int r=range.First;r<=range.Last;r++)Add((f.Origin.Y+r*f.StepY-a.Y)/dy);
        }
        var grid = cuts.ToArray();
        foreach (var (lo, hi) in grid.Zip(grid.Skip(1)))
        {
            token.ThrowIfCancellationRequested();
            double mid = (lo + hi) / 2;
            var p = new WorldPoint(a.X + dx * mid, a.Y + dy * mid);
            if (p.X < f.Origin.X || p.X > f.MaxX || p.Y < f.Origin.Y || p.Y > f.MaxY) continue;
            int col = Math.Min((int)((p.X - f.Origin.X) / f.StepX), f.Columns - 2);
            int row = Math.Min((int)((p.Y - f.Origin.Y) / f.StepY), f.Rows - 2);
            double denominator = dx / f.StepX - dy / f.StepY;
            if (denominator == 0) continue;
            double t = ((a.Y - f.Origin.Y) / f.StepY - row - (a.X - f.Origin.X) / f.StepX + col) / denominator;
            if (t > lo && t < hi) cuts.Add(t);
        }
        return cuts;
        void Add(double t) { token.ThrowIfCancellationRequested(); if (t > 0 && t < 1) cuts.Add(t); }
    }

    // Exact row/diagonal crossings within one vertical footprint interval.
    public IEnumerable<WorldHeightFieldColumnCut> ColumnCuts(double x,double low,double high,CancellationToken token)=>ColumnCutsCore(Geometry,x,low,high,token);
    internal static IEnumerable<WorldHeightFieldColumnCut> ColumnCutsCore(WorldHeightFieldGeometry f,double x,double low,double high,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!Number(x)||!Number(low)||!Number(high)||low>high)throw new ArgumentException("WORLD_SURFACE_COLUMN_INVALID");
        if (x < f.Origin.X || x > f.MaxX || high < f.Origin.Y || low > f.MaxY) yield break;
        int first = (int)Math.Clamp(Math.Floor((low-f.Origin.Y)/f.StepY), 0, f.Rows-1);
        int last = (int)Math.Clamp(Math.Ceiling((high-f.Origin.Y)/f.StepY), 0, f.Rows-1);
        double column = (x-f.Origin.X)/f.StepX;
        int col=(int)Math.Min(Math.Floor(column),f.Columns-2);
        double u = column-col;
        for (int row = first; row <= last; row++)
        {
            token.ThrowIfCancellationRequested();
            double y = f.Origin.Y+row*f.StepY;
            if (y >= low && y <= high) yield return new(y,0,col,row,false);
            double diagonal = y+u*f.StepY;
            if (row < f.Rows-1 && diagonal >= low && diagonal <= high) yield return new(diagonal,f.StepY/f.StepX,col,row,true);
        }
    }

    // Evaluate a source line from its original lattice identity, including exact
    // source endpoints. Reconstructing it from a slab midpoint invents tiny gaps.
    internal static double ColumnCutY(WorldHeightFieldGeometry f,WorldHeightFieldColumnCut cut,double x)
    {
        double bottom=f.Origin.Y+cut.Row*f.StepY;
        if(!cut.Diagonal||x==f.Origin.X+cut.Column*f.StepX)return bottom;
        if(x==f.Origin.X+(cut.Column+1)*f.StepX)return f.Origin.Y+(cut.Row+1)*f.StepY;
        return bottom+((x-f.Origin.X)/f.StepX-cut.Column)*f.StepY;
    }

    private static bool Id(string? s) => SettlementCatalogRegistry.ValidId(s);
    internal static bool Text(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 2048;
    internal static bool Number(double n) => double.IsFinite(n) && Math.Abs(n) <= 10000000;
    internal static bool Point(WorldPoint p) => Number(p.X) && Number(p.Y);
}

/// <summary>One triangle interpolation owner for elevation and scalar samples.</summary>
internal static class WorldTriangularInterpolation
{
    internal static WorldScalarSample Sample(IReadOnlyList<double> values,int columns,int col,int row,double u,double v,double stepX,double stepY)
    {
        int index=row*columns+col;
        double h00=values[index],h10=values[index+1],h01=values[index+columns],h11=values[index+columns+1];
        return Sample(h00,h10,h01,h11,u,v,stepX,stepY);
    }
    internal static WorldScalarSample Sample(double h00,double h10,double h01,double h11,double u,double v,double stepX,double stepY)
    {
        // On the diagonal choose the lower/right triangle; grid edges use increasing indices.
        double du=v<=u?h10-h00:h11-h01,dv=v<=u?h11-h10:h01-h00;
        return new(h00+du*u+dv*v,du/stepX,dv/stepY);
    }
}
