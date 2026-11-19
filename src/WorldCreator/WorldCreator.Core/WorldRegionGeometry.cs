using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Shared convex-piece union, connectivity and boundary geometry for regions and local allocations.</summary>
internal static class WorldRegionGeometry
{
    private const double Epsilon=1e-8;
    private sealed record Edge(int Cell, WorldPoint A, WorldPoint B, List<(double Start,double End)> Shared)
    {
        internal readonly Dictionary<double,WorldPoint> Endpoints=new(){{0,A},{1,B}};
    }
    internal sealed record Component(IReadOnlyList<WorldPolygon> Cells,IReadOnlyList<WorldRegionEdge> BoundaryEdges,double Area);
    internal static IReadOnlyList<WorldRegion> Assemble(WorldEnvironmentQuery environment,WorldSuitabilityProfile profile,
        IReadOnlyList<WorldPolygon> cells,IReadOnlyList<WorldRegionSeed> seeds,WorldSpatialWorkBudget budget,CancellationToken token,bool requireAllCells=false,bool allowDisconnected=false)
    {
        return FromComponents(Components(environment,profile,cells,budget,token),seeds,budget,requireAllCells,allowDisconnected);
    }
    internal static IReadOnlyList<WorldRegion> FromComponents(IReadOnlyList<Component> components,IReadOnlyList<WorldRegionSeed> seeds,WorldSpatialWorkBudget budget,bool requireAllCells=false,bool allowDisconnected=false)
    {
        if(allowDisconnected)
        {
            if(!requireAllCells||seeds.Count!=1||components.Count<1)throw new ArgumentException("WORLD_LAYOUT_DISCONNECTED_OWNERSHIP_INVALID");
            var seed=seeds[0];int matches=0;double area=0;
            foreach(var component in components)
            {
                budget.Charge(component.Cells.Sum(c=>c.Vertices.Count));area+=component.Area;
                if(component.Cells.Any(c=>c.ContainsPoint(seed.Position)))matches++;
            }
            if(matches!=1)throw new ArgumentException("WORLD_REGION_SEED_COMPONENT_AMBIGUOUS:"+seed.Id);
            if(area<seed.MinimumArea)throw new ArgumentException("WORLD_REGION_MINIMUM_AREA:"+seed.Id);
            return [new(seed.Id,seed,Array.AsReadOnly(components.SelectMany(c=>c.Cells).ToArray()),
                Array.AsReadOnly(components.SelectMany(c=>c.BoundaryEdges).ToArray()),area)];
        }
        var selected=new HashSet<int>();var regions=new List<WorldRegion>();
        foreach(var seed in seeds)
        {
            var matches=new List<int>();
            for(int i=0;i<components.Count;i++)
            {
                budget.Charge(components[i].Cells.Sum(c=>c.Vertices.Count));
                if(components[i].Cells.Any(c=>c.ContainsPoint(seed.Position)))matches.Add(i);
            }
            if(matches.Count!=1)throw new ArgumentException("WORLD_REGION_SEED_COMPONENT_AMBIGUOUS:"+seed.Id);
            int index=matches[0];if(!selected.Add(index))throw new ArgumentException("WORLD_REGION_SEEDS_SHARE_COMPONENT:"+seed.Id);
            var component=components[index];if(component.Area<seed.MinimumArea)throw new ArgumentException("WORLD_REGION_MINIMUM_AREA:"+seed.Id);
            regions.Add(new(seed.Id,seed,component.Cells,component.BoundaryEdges,component.Area));
        }
        if(requireAllCells&&selected.Count!=components.Count)throw new ArgumentException("WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
        return regions.AsReadOnly();
    }
    internal static IReadOnlyList<Component> Components(WorldEnvironmentQuery environment,WorldSuitabilityProfile profile,
        IReadOnlyList<WorldPolygon> cells,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var pointQuery=new WorldPointSuitability(environment,profile);
        var parents=Enumerable.Range(0,cells.Count).ToArray();
        // Cells retain their exact original coordinates and area. A shared face
        // already uses the owner's Epsilon predicate; preserve its source vertex
        // identity instead of independently interpolating two open outline ends.
        var aliases=new Dictionary<WorldPoint,WorldPoint>();
        WorldPoint Vertex(WorldPoint p)
        {while(aliases.TryGetValue(p,out var next)&&next!=p){budget.Charge();p=next;}return p;}
        void JoinVertex(WorldPoint a,WorldPoint b)
        {
            budget.Charge();if(a.DistanceTo(b)>Epsilon)return;a=Vertex(a);b=Vertex(b);if(a==b)return;
            if(a.X<b.X||a.X==b.X&&a.Y<b.Y)aliases[b]=a;else aliases[a]=b;
        }
        void Remember(Edge edge,double t,WorldPoint p)
        {if(edge.Endpoints.TryGetValue(t,out var original))JoinVertex(original,p);else edge.Endpoints.Add(t,p);}
        int Root(int x){while(parents[x]!=x){budget.Charge();x=parents[x];}return x;}
        var edges=cells.SelectMany((c,i)=>c.Vertices.Select((a,j)=>new Edge(i,a,c.Vertices[(j+1)%c.Vertices.Count],[]))).ToArray();
        long evaluationCost=1+environment.OrderedFields.Sum(f=>f.Terms.Count+1)+environment.Surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count)+
            environment.Surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count);
        // Mesh triangles share only nearby faces. Sweep conservative edge boxes,
        // retaining the exact collinear-overlap/eligibility predicate below.
        // Sampling cells are geometry data, never placement or street cells.
        budget.Charge((long)edges.Length*(1+(int)Math.Ceiling(Math.Log2(edges.Length+1d))));
        var sorted=edges.Select((e,i)=>(Edge:e,Index:i,MinX:Math.Min(e.A.X,e.B.X),MaxX:Math.Max(e.A.X,e.B.X),
            MinY:Math.Min(e.A.Y,e.B.Y),MaxY:Math.Max(e.A.Y,e.B.Y)))
            .OrderBy(e=>e.MinX).ThenBy(e=>e.Index).ToArray();
        var active=new List<int>();
        for(int i=0;i<sorted.Length;i++)
        {
            var current=sorted[i];
            for(int k=active.Count-1;k>=0;k--)
            {
                budget.Charge();var previous=sorted[active[k]];
                if(previous.MaxX<current.MinX-Epsilon){active.RemoveAt(k);continue;}
                if(previous.MaxY<current.MinY-Epsilon||current.MaxY<previous.MinY-Epsilon)continue;
                var a=previous.Edge;var b=current.Edge;if(a.Cell==b.Cell)continue;
                if(!Overlap(a,b,out double lo,out double hi))continue;
                // An eligible shared face establishes region topology only, not a width-qualified route.
                budget.Charge(evaluationCost);var middle=At(a,(lo+hi)/2);
                if(!pointQuery.Evaluate(middle,token).Eligible)continue;
                foreach(var av in new[]{a.A,a.B})foreach(var bv in new[]{b.A,b.B})JoinVertex(av,bv);
                WorldPoint Cut(double t)
                {
                    if(t==0)return a.A;if(t==1)return a.B;
                    return t==Parameter(a,b.A)?b.A:t==Parameter(a,b.B)?b.B:At(a,t);
                }
                var start=Cut(lo);var end=Cut(hi);Remember(a,lo,start);Remember(a,hi,end);
                parents[Root(b.Cell)]=Root(a.Cell);
                a.Shared.Add((lo,hi));double u=Parameter(b,At(a,lo)),v=Parameter(b,At(a,hi));
                double from=Math.Clamp(Math.Min(u,v),0,1),to=Math.Clamp(Math.Max(u,v),0,1);
                Remember(b,from,u<=v?start:end);Remember(b,to,u<=v?end:start);
                b.Shared.Add((from,to));
            }
            active.Add(i);
        }
        var byRoot=new Dictionary<int,List<int>>();var cellRoots=new int[cells.Count];
        for(int i=0;i<cells.Count;i++)
        {
            budget.Charge();int root=Root(i);cellRoots[i]=root;
            if(!byRoot.TryGetValue(root,out var members))byRoot.Add(root,members=[]);members.Add(i);
        }
        var edgesByRoot=new Dictionary<int,List<Edge>>();
        foreach(var edge in edges)
        {
            budget.Charge();int root=cellRoots[edge.Cell];
            if(!edgesByRoot.TryGetValue(root,out var members))edgesByRoot.Add(root,members=[]);members.Add(edge);
        }
        var components=new List<Component>();
        foreach(var group in byRoot)
        {
            var polygons=group.Value.Select(i=>cells[i]).ToArray();budget.Charge(polygons.Sum(p=>(long)p.Vertices.Count));
            double area=polygons.Sum(p=>Area(p.Vertices));
            var outline=new List<WorldRegionEdge>();
            foreach(var edge in edgesByRoot[group.Key])
            {
                double cursor=0;
                foreach(var interval in edge.Shared.OrderBy(x=>x.Start).ThenBy(x=>x.End))
                {
                    budget.Charge();if(interval.Start>cursor)Add(cursor,interval.Start);cursor=Math.Max(cursor,interval.End);
                }
                if(cursor<1)Add(cursor,1);
                void Add(double from,double to)
                {
                    var a=Vertex(At(edge,from));var b=Vertex(At(edge,to));
                    if(a.DistanceTo(b)>Epsilon)
                    {
                        if(outline.Count>=32768)throw new ArgumentException("WORLD_REGION_BOUNDARY_EDGE_BUDGET");
                        outline.Add(new(a,b));
                    }
                }
            }
            components.Add(new(Array.AsReadOnly(polygons),outline.AsReadOnly(),area));
        }
        budget.Charge(2L*edges.Length);
        return components.OrderBy(c=>c.Cells.SelectMany(p=>p.Vertices).Min(p=>p.X)).ThenBy(c=>c.Cells.SelectMany(p=>p.Vertices).Min(p=>p.Y)).ThenBy(c=>c.Area).ToArray();
    }

    // A closed footprint must be wholly inside the union. Exterior edges also catch enclosed
    // holes even when every footprint vertex happens to be on suitable land.
    internal static bool Contains(WorldRegion region,WorldPolygon polygon,WorldSpatialWorkBudget budget)
    {
        budget.Charge(polygon.Vertices.Count);
        double minX=polygon.Vertices.Min(p=>p.X),maxX=polygon.Vertices.Max(p=>p.X),minY=polygon.Vertices.Min(p=>p.Y),maxY=polygon.Vertices.Max(p=>p.Y);
        foreach(var p in polygon.Vertices)
        {
            budget.Charge(region.Cells.Sum(c=>c.Vertices.Count));
            if(!region.Cells.Any(c=>c.ContainsPoint(p)))return false;
        }
        foreach(var edge in region.BoundaryEdges)
        {
            budget.Charge();
            // A disjoint edge bounding box cannot cross the polygon interior. This exact
            // rejection avoids expensive intersection work against remote region boundaries.
            if(Math.Max(edge.From.X,edge.To.X)<minX||Math.Min(edge.From.X,edge.To.X)>maxX||
                Math.Max(edge.From.Y,edge.To.Y)<minY||Math.Min(edge.From.Y,edge.To.Y)>maxY)continue;
            budget.Charge(8L*polygon.Vertices.Count*polygon.Vertices.Count);
            if(polygon.CrossesInterior(edge.From,edge.To))return false;
        }
        return true;
    }
    internal static IReadOnlyList<WorldPoint> Clip(IReadOnlyList<WorldPoint> polygon,Func<WorldPoint,double> signed,WorldSpatialWorkBudget budget)
    {
        var result=ClipForMeasure(polygon,signed,budget);
        // Intersections at a rotated source endpoint can differ by floating-point roundoff.
        // Retain an existing computed vertex; never round coordinates to a layout grid.
        var unique=new List<WorldPoint>();
        foreach(var point in result)
            if(unique.Count==0||unique[^1].DistanceTo(point)>Epsilon)unique.Add(point);
        if(unique.Count>1&&unique[0].DistanceTo(unique[^1])<=Epsilon)unique.RemoveAt(unique.Count-1);
        if(Area(result)!=0&&Area(unique)==0)
        {
            var error=new ArgumentException("WORLD_REGION_GEOMETRY_RESOLUTION");
            error.Data["InputVertices"]=polygon.ToArray();error.Data["InputSignedValues"]=polygon.Select(signed).ToArray();
            error.Data["ClippedVertices"]=result.ToArray();error.Data["CleanedVertices"]=unique.ToArray();
            error.Data["SignedAreaBeforeCleanup"]=Area(result);error.Data["UsedWork"]=budget.Used;
            throw error;
        }
        return unique.AsReadOnly();
    }

    // Integration trials may cross an arbitrarily small corner before the final cut
    // is chosen. Measure its raw vertices; final topology still uses Clip above.
    internal static IReadOnlyList<WorldPoint> ClipForMeasure(IReadOnlyList<WorldPoint> polygon,Func<WorldPoint,double> signed,WorldSpatialWorkBudget budget)
    {
        var result=new List<WorldPoint>();
        for(int i=0;i<polygon.Count;i++)
        {
            budget.Charge();var a=polygon[i];var b=polygon[(i+1)%polygon.Count];double av=signed(a),bv=signed(b);
            if(av>=0)result.Add(a);
            if((av>=0)!=(bv>=0)){double t=av/(av-bv);result.Add(new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t));}
        }
        return result.AsReadOnly();
    }
    internal static double Area(IReadOnlyList<WorldPoint> p)
    {
        if(p.Count<3)return 0;var o=p[0];double sum=0;
        for(int i=1;i+1<p.Count;i++)sum+=(p[i].X-o.X)*(p[i+1].Y-o.Y)-(p[i].Y-o.Y)*(p[i+1].X-o.X);
        return sum/2;
    }
    private static WorldPoint At(Edge e,double t)=>e.Endpoints.TryGetValue(t,out var source)?source:new(e.A.X+(e.B.X-e.A.X)*t,e.A.Y+(e.B.Y-e.A.Y)*t);
    private static double Parameter(Edge e,WorldPoint p)
    {double x=e.B.X-e.A.X,y=e.B.Y-e.A.Y;return ((p.X-e.A.X)*x+(p.Y-e.A.Y)*y)/(x*x+y*y);}
    private static bool Overlap(Edge a,Edge b,out double lo,out double hi)
    {
        lo=hi=0;double x=a.B.X-a.A.X,y=a.B.Y-a.A.Y,length=a.A.DistanceTo(a.B);
        if(Math.Abs(x*(b.A.Y-a.A.Y)-y*(b.A.X-a.A.X))>Epsilon*length||
            Math.Abs(x*(b.B.Y-a.A.Y)-y*(b.B.X-a.A.X))>Epsilon*length)return false;
        double u=Parameter(a,b.A),v=Parameter(a,b.B);lo=Math.Max(0,Math.Min(u,v));hi=Math.Min(1,Math.Max(u,v));
        return (hi-lo)*length>Epsilon;
    }
    internal static WorldRegionEdge? Shared(WorldRegionEdge first,WorldRegionEdge second)
    {
        var a=new Edge(0,first.From,first.To,[]);var b=new Edge(1,second.From,second.To,[]);
        return Overlap(a,b,out double lo,out double hi)?new(At(a,lo),At(a,hi)):null;
    }
}
