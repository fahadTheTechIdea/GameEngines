using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Geometry;

/// <summary>A field value and purpose anchor select one connected contour, never a vacant position.</summary>
public sealed record WorldPopulationContour(string Id,double Value,WorldPoint Anchor,double MaximumAnchorDistance,
    int Count,double Spacing,double StartInset=0,double EndInset=0,bool Reverse=false);
public sealed record WorldContourPopulationPattern(string FieldId,IReadOnlyList<WorldPopulationContour> Contours);
public sealed record WorldContourPopulationResult(string Id,WorldPolyline Path,double AnchorDistance,bool Closed,IReadOnlyList<WorldPoint> Positions);

public static partial class WorldPopulationGeometry
{
    private const double ContourResolution=1e-8;
    public static IReadOnlyList<WorldContourPopulationResult> Contour(WorldRegion region,WorldEnvironmentQuery environment,
        WorldContourPopulationPattern pattern,int count,long maximumWork=5000000,CancellationToken token=default)
    {
        if(maximumWork is <1 or >200000000)throw new ArgumentException("WORLD_CONTOUR_WORK_INVALID");
        return Contour(region,environment,pattern,count,new WorldSpatialWorkBudget(maximumWork,token),token);
    }
    internal static IReadOnlyList<WorldContourPopulationResult> Contour(WorldRegion region,WorldEnvironmentQuery environment,
        WorldContourPopulationPattern pattern,int count,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(environment);
        if(region?.Cells is not {Count:>0 and <=4096}||pattern?.Contours is not {Count:>0 and <=32}||
            !environment.Definition.Fields.Any(f=>f.Id==pattern.FieldId)||count is <1 or >100000)
            throw new ArgumentException("WORLD_CONTOUR_PATTERN_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);long total=0;
        foreach(var c in pattern.Contours)
        {
            budget.Charge();
            if(c is null||!WorldEnvironmentQuery.Id(c.Id)||!ids.Add(c.Id)||!double.IsFinite(c.Value)||Math.Abs(c.Value)>1e9||
                !Finite(c.Anchor.X)||!Finite(c.Anchor.Y)||!Finite(c.MaximumAnchorDistance)||c.MaximumAnchorDistance<0||
                c.Count is <1 or >100000||!Finite(c.Spacing)||c.Spacing<=0||!Finite(c.StartInset)||c.StartInset<0||!Finite(c.EndInset)||c.EndInset<0)
                throw new ArgumentException("WORLD_CONTOUR_REQUEST_INVALID");
            total+=c.Count;
        }
        if(total!=count)throw new ArgumentException("WORLD_CONTOUR_COUNT_MISMATCH");
        // The existing surface sweep and environmental evaluator supply the same affine pieces
        // used by suitability and region generation. No raster or renderer-derived geometry.
        var pieces=new List<(WorldSurfaceSample Sample,WorldEnvironmentRegions.Piece Piece)>();
        var area=new WorldSurfaceAreaQuery(environment.Surface);
        foreach(var cell in region.Cells)
        {
            if(cell?.Vertices is not {Count:>=3 and <=64})throw new ArgumentException("WORLD_CONTOUR_CELL_INVALID");
            area.VisitRegions(cell,(sample,vertices)=>
            {
                if(sample.Status!=WorldSurfaceStatus.Sampled)return true;
                foreach(var piece in WorldEnvironmentRegions.Evaluate(environment,sample,vertices,4096,budget))
                {
                    if(pieces.Count>=16384)throw new ArgumentException("WORLD_CONTOUR_PIECE_BUDGET");pieces.Add((sample,piece));
                }
                return true;
            },budget,token);
        }
        var results=new List<WorldContourPopulationResult>();
        foreach(var contour in pattern.Contours)
        {
            var segments=new List<(WorldPoint A,WorldPoint B)>();
            foreach(var (sample,piece) in pieces)
            {
                var affine=piece.Fields[pattern.FieldId];var vertices=piece.Vertices;
                budget.Charge(vertices.Count);
                var values=vertices.Select(p=>WorldEnvironmentRegions.Value(affine,sample.Position,p)-contour.Value).ToArray();
                if(values.Min()>0||values.Max()<0)continue;
                if(affine.X==0&&affine.Y==0){if(values[0]==0)throw new ArgumentException("WORLD_CONTOUR_PLATEAU:"+contour.Id);continue;}
                var crossings=new List<WorldPoint>();
                for(int i=0;i<vertices.Count;i++)
                {
                    budget.Charge();int j=(i+1)%vertices.Count;double a=values[i],b=values[j];
                    if(a==0)crossings.Add(vertices[i]);
                    if(a<0&&b>0||a>0&&b<0){double t=a/(a-b);crossings.Add(new(vertices[i].X+t*(vertices[j].X-vertices[i].X),vertices[i].Y+t*(vertices[j].Y-vertices[i].Y)));}
                }
                var distinct=crossings.Distinct().ToArray();if(distinct.Length<2)continue;
                var sorted=distinct.OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
                // A tangent vertex can produce two roundoff-distinct crossings. It is a
                // point contact at the declared contour resolution, not a usable span.
                if(Distance(sorted[0],sorted[^1])<=ContourResolution)continue;
                if(segments.Count>=4096)throw new ArgumentException("WORLD_CONTOUR_SEGMENT_BUDGET");segments.Add((sorted[0],sorted[^1]));
            }
            var paths=ContourPaths(segments,budget);
            var choices=paths.Select(p=>{budget.Charge(p.Points.Count);return (Path:p,Distance:p.Points.Zip(p.Points.Skip(1),(a,b)=>PointSegment(contour.Anchor,a,b)).Min());}).OrderBy(c=>c.Distance).ToArray();
            if(choices.Length==0||choices[0].Distance>contour.MaximumAnchorDistance+ContourResolution)throw new ArgumentException("WORLD_CONTOUR_COMPONENT_MISSING:"+contour.Id);
            if(choices.Length>1&&Math.Abs(choices[1].Distance-choices[0].Distance)<=ContourResolution)throw new ArgumentException("WORLD_CONTOUR_COMPONENT_AMBIGUOUS:"+contour.Id);
            var chosen=choices[0];var path=contour.Reverse?new WorldPolyline(chosen.Path.Points.Reverse().ToArray()):chosen.Path;
            bool closed=path.Points[0]==path.Points[^1];
            double length=path.Points.Zip(path.Points.Skip(1),Distance).Sum();
            if(closed&&contour.StartInset+(contour.Count-1)*contour.Spacing>=length-contour.EndInset-ContourResolution)
                throw new ArgumentException("WORLD_CONTOUR_WRAP_FORBIDDEN");
            budget.Charge(contour.Count);
            var positions=Path(new(path,contour.Spacing,contour.StartInset,contour.EndInset),contour.Count,token);
            foreach(var position in positions)
            {
                budget.Charge(environment.Definition.Fields.Count+1);var actual=environment.Sample(position,token);
                if(!actual.Values.TryGetValue(pattern.FieldId,out double value)||Math.Abs(value-contour.Value)>1e-7*Math.Max(1,Math.Abs(contour.Value)))
                    throw new ArgumentException("WORLD_CONTOUR_FIELD_DISCONTINUITY");
            }
            results.Add(new(contour.Id,path,chosen.Distance,closed,positions));
        }
        return results.AsReadOnly();
    }

    private static IReadOnlyList<WorldPolyline> ContourPaths(IReadOnlyList<(WorldPoint A,WorldPoint B)> segments,WorldSpatialWorkBudget budget)
    {
        var nodes=new List<WorldPoint>();var cells=new Dictionary<(long X,long Y),List<int>>();
        int Node(WorldPoint point)
        {
            long x=(long)Math.Floor(point.X/ContourResolution),y=(long)Math.Floor(point.Y/ContourResolution);int? found=null;
            for(long dx=-1;dx<=1;dx++)for(long dy=-1;dy<=1;dy++)foreach(int id in cells.GetValueOrDefault((x+dx,y+dy),[]))
            {budget.Charge();if(Distance(nodes[id],point)<=ContourResolution){if(found is not null&&found!=id)throw new ArgumentException("WORLD_CONTOUR_GEOMETRY_RESOLUTION");found=id;}}
            if(found is {} current)return current;
            if(nodes.Count>=4096)throw new ArgumentException("WORLD_CONTOUR_VERTEX_BUDGET");
            int index=nodes.Count;nodes.Add(point);if(!cells.TryGetValue((x,y),out var list))cells[(x,y)]=list=[];list.Add(index);return index;
        }
        var ends=segments.OrderBy(s=>s.A.X).ThenBy(s=>s.A.Y).ThenBy(s=>s.B.X).ThenBy(s=>s.B.Y).Select(s=>(A:Node(s.A),B:Node(s.B))).ToArray();
        var edges=new HashSet<(int A,int B)>();
        // Split coincident seam segments at every endpoint before deduplication. Otherwise
        // equivalent terrain triangulations can create false branches or duplicate contours.
        foreach(var segment in ends)
        {
            var a=nodes[segment.A];var b=nodes[segment.B];double length=Distance(a,b);
            if(length<=ContourResolution)throw new ArgumentException("WORLD_CONTOUR_GEOMETRY_RESOLUTION");
            var splits=new List<(double T,int Id)>{(0,segment.A),(1,segment.B)};
            for(int i=0;i<nodes.Count;i++)
            {
                if(i==segment.A||i==segment.B)continue;
                budget.Charge();var p=nodes[i];double t=((p.X-a.X)*(b.X-a.X)+(p.Y-a.Y)*(b.Y-a.Y))/(length*length);
                if(t>0&&t<1&&PointSegment(p,a,b)<=ContourResolution)splits.Add((t,i));
            }
            splits.Sort((x,y)=>x.T.CompareTo(y.T));
            for(int i=1;i<splits.Count;i++){int u=splits[i-1].Id,v=splits[i].Id;if(u!=v)edges.Add(u<v?(u,v):(v,u));}
        }
        var neighbors=Enumerable.Range(0,nodes.Count).ToDictionary(i=>i,_=>new List<int>());
        foreach(var (a,b) in edges){budget.Charge();neighbors[a].Add(b);neighbors[b].Add(a);}
        if(neighbors.Values.Any(n=>n.Count>2))throw new ArgumentException("WORLD_CONTOUR_BRANCH_AMBIGUOUS");
        var remaining=new HashSet<int>(neighbors.Where(p=>p.Value.Count>0).Select(p=>p.Key));var paths=new List<WorldPolyline>();
        while(remaining.Count>0)
        {
            int first=remaining.OrderBy(i=>nodes[i].X).ThenBy(i=>nodes[i].Y).First();var component=new HashSet<int>();var pending=new Stack<int>();pending.Push(first);
            while(pending.TryPop(out int id)){budget.Charge();if(!component.Add(id))continue;foreach(int next in neighbors[id])pending.Push(next);}
            var endpoints=component.Where(i=>neighbors[i].Count==1).ToArray();bool closed=endpoints.Length==0;
            if(!closed&&endpoints.Length!=2)throw new ArgumentException("WORLD_CONTOUR_BRANCH_AMBIGUOUS");
            int start=(closed?component:endpoints.AsEnumerable()).OrderBy(i=>nodes[i].X).ThenBy(i=>nodes[i].Y).First();
            var points=new List<WorldPoint>{nodes[start]};int previous=-1,current=start;
            do
            {
                budget.Charge();remaining.Remove(current);
                var options=neighbors[current].Where(n=>n!=previous).OrderBy(n=>nodes[n].X).ThenBy(n=>nodes[n].Y).ToArray();if(options.Length==0)break;
                int next=options[0];points.Add(nodes[next]);previous=current;current=next;
            }while(current!=start);
            // Erase only numerical collinear seams; retain every actual bend.
            for(int i=points.Count-2;i>0;i--)if(PointSegment(points[i],points[i-1],points[i+1])<=1e-10){budget.Charge();points.RemoveAt(i);}
            paths.Add(new(points.AsReadOnly()));
        }
        return paths.AsReadOnly();
    }
    private static double Distance(WorldPoint a,WorldPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static double PointSegment(WorldPoint p,WorldPoint a,WorldPoint b)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,den=dx*dx+dy*dy;if(den==0)return Distance(p,a);
        double t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/den,0,1);return Distance(p,new(a.X+t*dx,a.Y+t*dy));
    }
}
