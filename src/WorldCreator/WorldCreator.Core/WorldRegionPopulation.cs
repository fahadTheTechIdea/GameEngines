using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Geometry;

/// <summary>One deliberate interior band relative to a generated region seed. SpanAnchor
/// selects the connected line interval; a hole cannot be skipped to fit more members.</summary>
public sealed record WorldRegionPopulationBand(string Id,int Count,double NormalOffset,double Spacing,
    double StartInset=0,double EndInset=0,double SpanAnchor=0);
public sealed record WorldRegionPopulationPattern(double DirectionDegrees,IReadOnlyList<WorldRegionPopulationBand> Bands);

public static partial class WorldPopulationGeometry
{
    public static IReadOnlyList<WorldPoint> Region(WorldRegion region,WorldRegionPopulationPattern pattern,int count,CancellationToken token=default)
        =>Region(region,pattern,count,new WorldSpatialWorkBudget(5000000,token),token);

    internal static IReadOnlyList<WorldPoint> Region(WorldRegion region,WorldRegionPopulationPattern pattern,int count,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(region?.Seed is null||region.Cells is not {Count:>0 and <=4096}||!Finite(region.Seed.Position.X)||!Finite(region.Seed.Position.Y)||
            pattern?.Bands is not {Count:>0 and <=32}||!double.IsFinite(pattern.DirectionDegrees)||Math.Abs(pattern.DirectionDegrees)>360||count is <1 or >100000)
            throw new ArgumentException("WORLD_REGION_POPULATION_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);long total=0;
        foreach(var band in pattern.Bands)
        {
            budget.Charge();
            if(band is null||!WorldEnvironmentQuery.Id(band.Id)||!ids.Add(band.Id)||band.Count is <1 or >100000||
                !Finite(band.NormalOffset)||!Finite(band.Spacing)||band.Spacing<=0||!Finite(band.StartInset)||band.StartInset<0||
                !Finite(band.EndInset)||band.EndInset<0||!Finite(band.SpanAnchor))throw new ArgumentException("WORLD_REGION_POPULATION_BAND_INVALID");
            total+=band.Count;
        }
        if(total!=count)throw new ArgumentException("WORLD_REGION_POPULATION_COUNT_MISMATCH");
        // Region cells are convex arrangement pieces, not placement cells. Validate public inputs
        // before intersecting; metadata additionally replays their complete generated provenance.
        foreach(var cell in region.Cells)
        {
            if(cell?.Vertices is not {Count:>=3 and <=4096}||cell.Vertices.Any(p=>!Finite(p.X)||!Finite(p.Y)))
                throw new ArgumentException("WORLD_REGION_POPULATION_GEOMETRY_INVALID");
            budget.Charge(4L*cell.Vertices.Count*cell.Vertices.Count);cell.Validate();
            double sign=Math.Sign(cell.SignedArea());
            for(int i=0;i<cell.Vertices.Count;i++)
            {
                budget.Charge();var a=cell.Vertices[i];var b=cell.Vertices[(i+1)%cell.Vertices.Count];var c=cell.Vertices[(i+2)%cell.Vertices.Count];
                if(sign*((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))<0)throw new ArgumentException("WORLD_REGION_POPULATION_CELL_NOT_CONVEX");
            }
        }
        double angle=pattern.DirectionDegrees*Math.PI/180,dx=Math.Cos(angle),dy=Math.Sin(angle);
        var output=new List<WorldPoint>(count);
        foreach(var band in pattern.Bands)
        {
            var origin=region.Seed.Position.Offset(-dy*band.NormalOffset,dx*band.NormalOffset);
            var intervals=new List<(double Start,double End)>();
            foreach(var cell in region.Cells)
            {
                double lo=double.NegativeInfinity,hi=double.PositiveInfinity,sign=Math.Sign(cell.SignedArea());bool outside=false;
                for(int i=0;i<cell.Vertices.Count;i++)
                {
                    budget.Charge();var a=cell.Vertices[i];var b=cell.Vertices[(i+1)%cell.Vertices.Count];
                    double ex=b.X-a.X,ey=b.Y-a.Y;
                    double constant=sign*(ex*(origin.Y-a.Y)-ey*(origin.X-a.X));
                    double coefficient=sign*(ex*dy-ey*dx);
                    if(coefficient==0){if(constant<0){outside=true;break;}continue;}
                    // The same shared edge is traversed in opposite directions by adjacent
                    // cells. Compute its crossing from one canonical endpoint order so
                    // floating-point evaluation cannot manufacture a gap between them.
                    // This never merges distinct boundaries or relaxes a true gap.
                    var first=a;var last=b;
                    if(a.X>b.X||a.X==b.X&&a.Y>b.Y){first=b;last=a;}
                    double cx=last.X-first.X,cy=last.Y-first.Y;
                    double canonicalConstant=cx*(origin.Y-first.Y)-cy*(origin.X-first.X);
                    double canonicalCoefficient=cx*dy-cy*dx;
                    double boundary=-canonicalConstant/canonicalCoefficient;
                    if(coefficient>0)lo=Math.Max(lo,boundary);else hi=Math.Min(hi,boundary);
                }
                if(!outside&&double.IsFinite(lo)&&double.IsFinite(hi)&&hi-lo>1e-9)intervals.Add((lo,hi));
            }
            // Only adjacent/touching arrangement pieces merge. No positive-width gap is bridged.
            var spans=new List<(double Start,double End)>();
            foreach(var span in intervals.OrderBy(s=>s.Start).ThenBy(s=>s.End))
            {
                budget.Charge();
                if(spans.Count>0&&span.Start<=spans[^1].End)spans[^1]=(spans[^1].Start,Math.Max(spans[^1].End,span.End));
                else spans.Add(span);
            }
            var selected=spans.Where(s=>band.SpanAnchor>s.Start&&band.SpanAnchor<s.End).ToArray();
            if(selected.Length!=1)throw new ArgumentException("WORLD_REGION_POPULATION_SPAN_MISSING:"+band.Id);
            var chosen=selected[0];
            budget.Charge(band.Count);
            var path=new WorldPolyline([origin.Offset(dx*chosen.Start,dy*chosen.Start),origin.Offset(dx*chosen.End,dy*chosen.End)]);
            output.AddRange(Path(new(path,band.Spacing,band.StartInset,band.EndInset),band.Count,token));
        }
        return output.AsReadOnly();
    }
    private static bool Finite(double value)=>double.IsFinite(value)&&Math.Abs(value)<=1e7;
}
