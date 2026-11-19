using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

/// <summary>Structured sweep over authored boundaries and local field cross-sections. Predicates
/// must be constant within each terrain patch/triangle (kind or slope, not varying height).</summary>
public sealed class WorldSurfaceAreaQuery
{
    private readonly record struct SectionLine(double Slope,WorldPoint? From=null,WorldPoint? To=null,WorldHeightFieldColumnCut? Field=null);
    private readonly WorldSurfaceQuery surface;
    private readonly (WorldPoint A, WorldPoint B)[] edges;
    private readonly int sampleCost;
    private readonly bool includePhysical;

    public WorldSurfaceAreaQuery(WorldSurfaceQuery surface)
        :this(surface,true){}
    internal WorldSurfaceAreaQuery(WorldSurfaceQuery surface,bool includePhysical)
    {
        this.includePhysical=includePhysical;
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        var d = surface.Definition;
        edges = (d.Boundary is {} boundary ? new[] { boundary } : [])
            .Concat((includePhysical?surface.Exclusions:d.Exclusions).Select(e => e.Boundary)).Concat(d.Patches.Select(p => p.Boundary))
            .SelectMany(Edges).ToArray();
        sampleCost = Math.Max(1, edges.Length);
    }

    public bool Matches(WorldPolygon area, Func<WorldSurfaceSample, bool> predicate, CancellationToken token = default)
        => Matches(area, predicate, new WorldSpatialWorkBudget(20000000, token), token);

    internal bool Matches(WorldPolygon area, Func<WorldSurfaceSample, bool> predicate,
        WorldSpatialWorkBudget budget, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Walk(area,(sample,_)=>predicate(sample),false,budget,token,constantTerrain:true);
    }

    internal bool VisitRegions(WorldPolygon area,Func<WorldSurfaceSample,IReadOnlyList<WorldPoint>,bool> visitor,
        WorldSpatialWorkBudget budget,CancellationToken token) => Walk(area,visitor,true,budget,token);

    // Boolean consumers need exact covered geometry, but not the historical
    // decomposition representatives that placement algorithms use as candidates.
    internal bool VisitLocalRegions(WorldPolygon area,Func<WorldSurfaceSample,IReadOnlyList<WorldPoint>,bool> visitor,
        WorldSpatialWorkBudget budget,CancellationToken token) => Walk(area,visitor,true,budget,token,localGeometry:true);

    private bool Walk(WorldPolygon area,Func<WorldSurfaceSample,IReadOnlyList<WorldPoint>,bool> visitor,bool includeGeometry,
        WorldSpatialWorkBudget budget,CancellationToken token,bool constantTerrain=false,bool localGeometry=false)
    {
        if (area?.Vertices is null || area.Vertices.Count is < 3 or > 64 || area.Vertices.Any(p =>
            !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X)>10000000 || Math.Abs(p.Y)>10000000))
            throw new ArgumentException("WORLD_SURFACE_AREA_INVALID");
        token.ThrowIfCancellationRequested();
        area.Validate();
        double minX=area.Vertices.Min(p=>p.X), maxX=area.Vertices.Max(p=>p.X);
        double minY=area.Vertices.Min(p=>p.Y), maxY=area.Vertices.Max(p=>p.Y);
        budget.Charge(edges.Length);
        var areaEdges=Edges(area).ToArray();var crossing=new List<(WorldPoint A,WorldPoint B)>();
        foreach(var e in edges)
        {
            if(Math.Min(e.A.X,e.B.X)>maxX||Math.Max(e.A.X,e.B.X)<minX||Math.Min(e.A.Y,e.B.Y)>maxY||Math.Max(e.A.Y,e.B.Y)<minY)continue;
            // Geometry-producing callers use the original complete event set:
            // subdivision representatives are intentional placement candidates.
            // Heightfield cuts also retain their original authored event anchors.
            if((!constantTerrain&&!localGeometry)||surface.HeightField is not null){crossing.Add(e);continue;}
            // A long diagonal envelope has a large bounding box. Edges outside
            // the actual queried polygon cannot change its interior samples.
            // Full source sampling remains unchanged, including an exclusion
            // that completely contains the query without crossing its boundary.
            budget.Charge(8L*area.Vertices.Count);
            if(area.ContainsPoint(e.A)||area.ContainsPoint(e.B)||areaEdges.Any(a=>
                PlanarIntersections.Intersect(e.A,e.B,a.A,a.B).Kind!=IntersectionKind.None))crossing.Add(e);
        }
        if(constantTerrain&&crossing.Count==0&&surface.HeightField is null)
        {
            // Kind and planar gradient are constant when no source boundary
            // crosses this connected query. Position/elevation predicates and
            // callers requiring geometry must retain the full decomposition.
            budget.Charge(sampleCost+1);
            bool matches=visitor(includePhysical?surface.Sample(area.Vertices[0],token):surface.SampleTerrain(area.Vertices[0],token),Array.Empty<WorldPoint>());
            token.ThrowIfCancellationRequested();return matches;
        }
        var relevant=crossing.Distinct().Concat(areaEdges).ToArray();
        if(relevant.Length>512)throw new ArgumentException("WORLD_SURFACE_AREA_EDGE_BUDGET");
        var xs=new SortedSet<double>{minX,maxX};
        // Authored event coordinates remain distinct. Only calculated crossings
        // are canonicalized at floating-point operation precision; this avoids
        // inventing sliver faces when a computed crossing meets an existing seam.
        double scale=Math.Max(1,relevant.SelectMany(e=>new[]{Math.Abs(e.A.X),Math.Abs(e.A.Y),Math.Abs(e.B.X),Math.Abs(e.B.Y)}).Max());
        double roundoff=4*(Math.BitIncrement(scale)-scale);
        void X(double x)
        {
            if(x>=minX && x<=maxX)xs.Add(x);
            if(xs.Count>4096)throw new ArgumentException("WORLD_SURFACE_AREA_EVENT_BUDGET");
        }
        foreach(var e in relevant){X(e.A.X);X(e.B.X);}
        if(surface.HeightField is {} anchorField)
        {
            var f=anchorField.Geometry;var range=WorldHeightFieldQuery.CutRange(f.Origin.X,f.StepX,f.Columns,minX,maxX);
            budget.Charge(range.Last-range.First+1);
            for(int col=range.First;col<=range.Last;col++)X(f.Origin.X+col*f.StepX);
        }
        var authoredX=new SortedSet<double>(xs);
        void CrossingX(double x)=>X(WorldGeometryEvents.Canonical(x,xs,authoredX,roundoff,budget));
        if (surface.HeightField is {} field)
        {
            var f = field.Geometry;
            // Field edges only meet each other at mesh vertices. Their remaining ordering events
            // are intersections with authored/query boundaries; no mesh-edge pair expansion.
            foreach (var edge in relevant)
            {
                budget.Charge(WorldHeightFieldQuery.CutWork(f,edge.A,edge.B));
                foreach (double t in field.Cuts(edge.A, edge.B, token)) CrossingX(edge.A.X + (edge.B.X - edge.A.X) * t);
            }
        }
        // Between these events no pair of boundary edges changes vertical ordering.
        for(int i=0;i<relevant.Length;i++)for(int j=i+1;j<relevant.Length;j++)
        {
            budget.Charge();
            var hit=PlanarIntersections.Intersect(relevant[i].A,relevant[i].B,relevant[j].A,relevant[j].B);
            if(hit.Point is {} p)CrossingX(p.X);
        }
        var events=xs.ToArray();
        var authoredY=new SortedSet<double>(relevant.SelectMany(e=>new[]{e.A.Y,e.B.Y}).Where(y=>y>=minY&&y<=maxY)){minY,maxY};
        if(surface.HeightField is {} yField)
        {
            var f=yField.Geometry;var range=WorldHeightFieldQuery.CutRange(f.Origin.Y,f.StepY,f.Rows,minY,maxY);
            budget.Charge(range.Last-range.First+1);
            for(int row=range.First;row<=range.Last;row++)
            {double y=f.Origin.Y+row*f.StepY;if(y>=minY&&y<=maxY)authoredY.Add(y);}
        }
        if(events.Length>4096)throw new ArgumentException("WORLD_SURFACE_AREA_EVENT_BUDGET");
        // Check both every open arrangement face and its boundary, including vertical edges.
        foreach(var slab in events.Select(x=>(Left:x,Right:x)).Concat(events.Zip(events.Skip(1),(a,b)=>(Left:a,Right:b))))
        {
            double x=(slab.Left+slab.Right)/2;
            var ys=new SortedDictionary<double,SectionLine>{{minY,new(0)},{maxY,new(0)}};
            var yEvents=new SortedSet<double>{minY,maxY};
            void Y(double y,double slope=0,WorldPoint? from=null,WorldPoint? to=null,WorldHeightFieldColumnCut? field=null)
            {
                y=WorldGeometryEvents.Canonical(y,yEvents,authoredY,roundoff,budget);
                if(y>=minY && y<=maxY){ys.TryAdd(y,new(slope,from,to,field));yEvents.Add(y);}
            }
            foreach(var e in relevant)
            {
                budget.Charge();
                if(x<Math.Min(e.A.X,e.B.X) || x>Math.Max(e.A.X,e.B.X))continue;
                if(e.A.X==e.B.X){Y(e.A.Y);Y(e.B.Y);}
                else {double slope=(e.B.Y-e.A.Y)/(e.B.X-e.A.X);Y(e.A.Y+(x-e.A.X)*slope,slope,e.A,e.B);}
            }
            if (surface.HeightField is {} heightField)
            {
                // Only enumerate rows crossed by this footprint at x, including each disconnected
                // interval of a concave polygon. Outer min/max bounds alone would scan vacant gaps.
                var section = new SortedSet<double>();
                foreach (var edge in Edges(area))
                {
                    budget.Charge();
                    if (x < Math.Min(edge.A.X,edge.B.X) || x > Math.Max(edge.A.X,edge.B.X)) continue;
                    if (edge.A.X == edge.B.X) { section.Add(edge.A.Y); section.Add(edge.B.Y); }
                    else section.Add(edge.A.Y + (x-edge.A.X)/(edge.B.X-edge.A.X)*(edge.B.Y-edge.A.Y));
                }
                var limits = section.ToArray();
                foreach (var (lo,hi) in limits.Zip(limits.Skip(1)))
                {
                    budget.Charge(area.Vertices.Count);
                    if (!area.ContainsPoint(new(x,(lo+hi)/2))) continue;
                    foreach (var cut in heightField.ColumnCuts(x,lo,hi,token)) { budget.Charge(); Y(cut.Y,cut.Slope,field:cut); }
                }
            }
            var cuts=ys.ToArray();
            foreach(var interval in cuts.Select(c=>(Low:c,High:c)).Concat(cuts.Zip(cuts.Skip(1),(a,b)=>(Low:a,High:b))))
            {
                double y=(interval.Low.Key+interval.High.Key)/2;
                budget.Charge(area.Vertices.Count);
                var point=new WorldPoint(x,y);
                if(!area.ContainsPoint(point))continue;
                budget.Charge(sampleCost);
                IReadOnlyList<WorldPoint> geometry=[];
                if(includeGeometry)
                {
                    budget.Charge(4);
                    WorldPoint At(double boundaryX,KeyValuePair<double,SectionLine> line)
                    {
                        // Calculated crossings at a slab edge need the same
                        // operation-precision identity as ordering events. In
                        // particular a source diagonal meeting y=0 must not
                        // create a tiny outside-domain corner. Distinct authored
                        // coordinates remain protected by Canonical.
                        WorldPoint Calculated(double value)=>new(boundaryX,
                            WorldGeometryEvents.Canonical(value,new SortedSet<double>(),authoredY,roundoff,budget));
                        if(line.Value.Field is {} cut)return Calculated(WorldHeightFieldQuery.ColumnCutY(surface.HeightField!.Geometry,cut,boundaryX));
                        if(line.Value.From is {} a)
                        {
                            if(boundaryX==a.X)return new(boundaryX,a.Y);
                            if(line.Value.To is {} b&&boundaryX==b.X)return new(boundaryX,b.Y);
                            return Calculated(a.Y+(boundaryX-a.X)*line.Value.Slope);
                        }
                        return Calculated(line.Key+(boundaryX-x)*line.Value.Slope);
                    }
                    geometry=Array.AsReadOnly(new[]{At(slab.Left,interval.Low),At(slab.Right,interval.Low),
                        At(slab.Right,interval.High),At(slab.Left,interval.High)}.Distinct().ToArray());
                }
                bool matches=visitor(includePhysical?surface.Sample(point,token):surface.SampleTerrain(point,token),geometry);
                token.ThrowIfCancellationRequested();if(!matches)return false;
            }
        }
        token.ThrowIfCancellationRequested();
        return true;
    }

    private static IEnumerable<(WorldPoint A,WorldPoint B)> Edges(WorldPolygon p)
        => p.Vertices.Select((a,i)=>(a,p.Vertices[(i+1)%p.Vertices.Count]));

}

/// <summary>Operation-precision identity for calculated coordinates. Distinct
/// original anchors are never merged, even when they are closer than tolerance.</summary>
internal static class WorldGeometryEvents
{
    internal static double Canonical(double value,SortedSet<double> existing,SortedSet<double> authored,double tolerance,WorldSpatialWorkBudget budget)
    {
        budget.Charge();if(authored.Contains(value))return value;
        double? Nearest(SortedSet<double> candidates)
        {
            double? best=null;double distance=double.PositiveInfinity;
            foreach(double candidate in candidates.GetViewBetween(value-tolerance,value+tolerance))
            {budget.Charge();double d=Math.Abs(candidate-value);if(d<distance){best=candidate;distance=d;}}
            return best;
        }
        return Nearest(authored)??Nearest(existing)??value;
    }
}
