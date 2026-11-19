using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPhysicalRoutingTests
{
    private readonly record struct Rect(double X,double Y,double W,double H);
    private static WorldPolygon Box(Rect r)=>new([new(r.X,r.Y),new(r.X+r.W,r.Y),new(r.X+r.W,r.Y+r.H),new(r.X,r.Y+r.H)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Physical routing: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldSurfaceDefinition Surface()=>new(1,"physical-route-world","meters",Box(new(0,0,300,500)),[],[new("ground","ground","Shared terrain","test:terrain",Box(new(0,0,300,500)),0,new(0,0),0,0,0)]);
    private static WorldTraversalProfile Profile()=>new("walk","Required full-width passage","test:walk",.5,0,new Dictionary<string,double>{{"ground",1}});
    private static WorldRouteRequest Request()=>new("journey","Search a new detour","test:journey",new(10,20),new(90,20),Profile(),new(.2,[]));
    private static Rect[] Rectangles(int count,bool secondary=false)=>Enumerable.Range(0,count).Select(i=>i==0?new Rect(48,5,4,30):secondary&&i==1?new Rect(28,9,8,6):new Rect(120+(i-1)%20*5,80+(i-1)/20*5,2,2)).ToArray();
    private static WorldSurfaceExclusion[] Obstacles(Rect[] rectangles)=>rectangles.Select((r,i)=>new WorldSurfaceExclusion("solid-"+i.ToString("D4"),"Required solid "+i,"test:solid",Box(r))).ToArray();
    public static void Geometry()
    {
        var planner=new WorldSurfaceRoutePlanner();var request=Request();
        foreach(int count in new[]{257,512,1024})
        {
            var rectangles=Rectangles(count);var query=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles(rectangles));
            Check(query.Definition.Exclusions.Count==0&&query.PhysicalObstacles.Count==count&&query.Exclusions.Count==count,"base masks and every physical obstacle remain distinct");
            Check(query.Sample(new(50,20)).Status==WorldSurfaceStatus.Excluded,"physical point rejection");
            Check(query.Trace(request.From,request.To).Any(s=>s.Sample.Status==WorldSurfaceStatus.Excluded),"physical trace partition");
            Check(!query.ContainsCorridor(new([request.From,request.To]),.5),"physical full-width corridor rejection");
            Check(!new WorldSurfaceAreaQuery(query).Matches(Box(new(49,19,2,2)),s=>s.Status==WorldSurfaceStatus.Sampled),"physical area rejection");
            var result=planner.Plan(query,request);Check(result.Status==WorldRouteStatus.Found&&result.Path!.Points.Count>=4,"new multi-edge detour through "+count+" solids");
            Check(result.CandidateAudit is {CompleteWinningSet:true,Unexamined:0,BoundPruned:>0}&&result.CandidateCount<=256,"complete winning-set compaction under original candidate limit");
            var audit=result.CandidateAudit!;Check(audit.Proposals==audit.Retained+audit.BoundPruned+audit.GeometryRejected+audit.Unexamined,"proposal accounting");
            Independent(result.Path!,rectangles,.5);Console.WriteLine($"Qualified {count} solids: {result.CandidateCount}/{audit.Proposals} candidates, {audit.BoundPruned} cost-pruned, {result.EdgeChecks} edges, {result.Work} work, cost {result.Cost!.Total:F9}.");
            Check(planner.Compile(query,request,result.Path!).Status==WorldRouteStatus.Found,"high-solid-count authored trace uses every physical obstacle");
            Check(planner.Compile(query,request,new([request.From,request.To])).Status==WorldRouteStatus.TraceRejected,"authored trace cannot bypass solid geometry");
        }
        var networkQuery=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles(Rectangles(257)));
        var network=new WorldDestinationNetworkPlanner().Plan(networkQuery,new("network",[new("a","Required origin",request.From),new("b","Required goal",request.To)],
            new("Connect required destinations","test:network","region",request.Profile,request.Search,NearestNeighbors:1,RequiredConnections:[new("a","b")])));
        Check(network.Status==WorldNetworkStatus.Found&&network.Edges.Count==1&&network.Edges[0].Reason=="required","new bidirectional network through 257 solids");
        Independent(network.Edges[0].Forward.Path!,Rectangles(257),.5);Independent(network.Edges[0].Reverse.Path!,Rectangles(257),.5);
        foreach(bool secondary in new[]{false,true})
        {
            var rectangles=Rectangles(40,secondary);var query=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles(rectangles));
            var result=planner.Plan(query,request);double oracle=Dijkstra(rectangles,request);
            Check(result.Status==WorldRouteStatus.Found&&Math.Abs(result.Cost!.Total-oracle)<1e-7,"independent full-feature Dijkstra matches compacted lowest cost");
            Independent(result.Path!,rectangles,.5);
        }
        var firstQuery=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles(Rectangles(512,true)));
        var first=planner.Plan(firstQuery,request with{Search=request.Search with{Objective=WorldRouteObjective.FirstFeasible}});
        Check(first.Status==WorldRouteStatus.Found&&first.CandidateAudit is {CompleteWinningSet:false,Unexamined:>0},"first-feasible seed path makes no minimum-cost claim");Independent(first.Path!,Rectangles(512,true),.5);
        var nearCorner=new Rect(40.4,20.4,.1,.1);var corridorRects=new[]{nearCorner}.Concat(Rectangles(256)).ToArray();
        var corridorQuery=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles(corridorRects));
        var journey=new WorldCorridorJourneyInput("short","short","Full-width endpoint circle","test:circle",new([new(10,20),new(40,20)]),Profile(),true);
        Check(new WorldCorridorPlanner().Plan(corridorQuery,new("short","walk","test:circle",0,"connect-at-grade"),[journey]).Junctions.Count==2,"physical query survives corridor compilation and exact endpoint circles");
        var obstructed=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles([new(40.3,20.3,.1,.1),..Rectangles(256)]));
        Reject(()=>new WorldCorridorPlanner().Plan(obstructed,new("short","walk","test:circle",0,"connect-at-grade"),[journey]),"WORLD_CORRIDOR_SEGMENT_UNRESOLVED");
        var source=Obstacles(Rectangles(257));var vertices=source[0].Boundary.Vertices.ToList();source[0]=source[0] with{Boundary=new(vertices)};
        var snapshot=new WorldSurfaceQuery(Surface(),physicalObstacles:source);vertices[0]=new(200,200);source[0]=source[0] with{Purpose="Mutated input"};
        Check(snapshot.Sample(new(50,20)).Status==WorldSurfaceStatus.Excluded&&snapshot.PhysicalObstacles[0].Purpose!="Mutated input","physical snapshots detach caller lists and records");
        Reject(()=>new WorldSurfaceQuery(Surface() with{Exclusions=Obstacles(Rectangles(257))}),"WORLD_SURFACE_DEFINITION_INVALID");
        Reject(()=>new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles(Rectangles(1025))),"WORLD_SURFACE_DEFINITION_INVALID");
        Reject(()=>new WorldSurfaceQuery(Surface(),physicalObstacles:[source[1],source[1]]),"WORLD_SURFACE_PHYSICAL_OBSTACLE_INVALID");
        WorldPolygon Polygon(int count)=>new(Enumerable.Range(0,count).Select(i=>new WorldPoint(200+Math.Cos(2*Math.PI*i/count),300+Math.Sin(2*Math.PI*i/count))).ToArray());
        var atLimit=Enumerable.Range(0,1024).Select(i=>new WorldSurfaceExclusion("limit-"+i,"Bounded solid","test:vertices",Polygon(i==1023?8:16))).ToArray();
        Check(new WorldSurfaceQuery(Surface(),physicalObstacles:atLimit).PhysicalObstacles.Count==1024,"exact combined 16384-vertex bound");
        Reject(()=>new WorldSurfaceQuery(Surface(),physicalObstacles:atLimit.Select((p,i)=>i==1023?p with{Boundary=Polygon(16)}:p).ToArray()),"WORLD_SURFACE_GEOMETRY_BUDGET_OR_INPUT_INVALID");
        try{planner.Plan(snapshot,request with{Search=request.Search with{MaximumCandidates=2}});throw new Exception("Expected route candidate budget");}
        catch(ArgumentException error) when(error.Message=="WORLD_ROUTE_CANDIDATE_BUDGET")
        {
            Check(error.Data["RouteCandidateAudit"] is WorldRouteCandidateAudit audit&&audit.Retained==3&&
                audit.Proposals==audit.Retained+audit.BoundPruned+audit.GeometryRejected+audit.Unexamined&&
                !audit.CompleteWinningSet&&Equals(error.Data["RouteRequestId"],request.Id),"bounded refusal records complete candidate accounting and route identity");
        }
        Reject(()=>planner.Plan(snapshot,request with{Search=request.Search with{MaximumWork=1}}),"WORLD_ROUTE_WORK_BUDGET");
        var closed=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles([new(48,0,4,500)]));
        Check(planner.Plan(closed,request).Status==WorldRouteStatus.CandidateGraphDisconnected,"closed barrier does not become a path");
        var endpoint=new WorldSurfaceQuery(Surface(),physicalObstacles:Obstacles([new(9,19,2,2),..Rectangles(256)]));
        Check(planner.Plan(endpoint,request).Status==WorldRouteStatus.EndpointRejected,"all obstacles still reject occupied endpoints");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(snapshot,request,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Console.WriteLine("PASS: 257/512/1024 separate solids, independent capsule geometry and full-feature Dijkstra, secondary obstacles, snapshots, authored/physical/candidate/work bounds, closed barrier, blocked endpoint and cancellation.");
    }
    private static double Distance(WorldPoint p,WorldPoint a,WorldPoint b)
    {
        double x=b.X-a.X,y=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y),0,1);
        return Math.Sqrt(Math.Pow(p.X-a.X-t*x,2)+Math.Pow(p.Y-a.Y-t*y,2));
    }
    private static bool Segment(WorldPoint a,WorldPoint b,Rect[] rectangles,double radius)
    {
        if(Math.Min(a.X,b.X)<=radius||Math.Max(a.X,b.X)>=300-radius||Math.Min(a.Y,b.Y)<=radius||Math.Max(a.Y,b.Y)>=500-radius)return false;
        foreach(var r in rectangles)
        {
            double lo=0,hi=1;
            bool Slab(double origin,double delta,double min,double max)
            {
                if(delta==0)return origin>=min&&origin<=max;
                double p=(min-origin)/delta,q=(max-origin)/delta;lo=Math.Max(lo,Math.Min(p,q));hi=Math.Min(hi,Math.Max(p,q));return lo<=hi;
            }
            if(Slab(a.X,b.X-a.X,r.X,r.X+r.W)&&Slab(a.Y,b.Y-a.Y,r.Y,r.Y+r.H))return false;
            double End(WorldPoint p)=>Math.Sqrt(Math.Pow(Math.Max(Math.Max(r.X-p.X,0),p.X-r.X-r.W),2)+Math.Pow(Math.Max(Math.Max(r.Y-p.Y,0),p.Y-r.Y-r.H),2));
            double distance=Math.Min(End(a),End(b));
            foreach(var p in new[]{new WorldPoint(r.X,r.Y),new(r.X+r.W,r.Y),new(r.X+r.W,r.Y+r.H),new(r.X,r.Y+r.H)})distance=Math.Min(distance,Distance(p,a,b));
            if(distance<=radius+1e-8)return false;
        }
        return true;
    }
    private static void Independent(WorldPolyline path,Rect[] rectangles,double radius)
    {for(int i=1;i<path.Points.Count;i++)Check(Segment(path.Points[i-1],path.Points[i],rectangles,radius),"independent full-radius rectangle clearance");}
    private static double Dijkstra(Rect[] rectangles,WorldRouteRequest request)
    {
        var points=new HashSet<WorldPoint>();double offset=(request.Profile.Radius+request.Search.Clearance)*Math.Sqrt(2);
        foreach(var r in rectangles.Prepend(new(0,0,300,500)))foreach(var v in new[]{new WorldPoint(r.X,r.Y),new(r.X+r.W,r.Y),new(r.X+r.W,r.Y+r.H),new(r.X,r.Y+r.H)})
            for(int i=0;i<8;i++){var p=new WorldPoint(v.X+offset*Math.Cos(i*Math.PI/4),v.Y+offset*Math.Sin(i*Math.PI/4));if(p.X>=0&&p.X<=300&&p.Y>=0&&p.Y<=500&&!rectangles.Any(r=>p.X>=r.X&&p.X<=r.X+r.W&&p.Y>=r.Y&&p.Y<=r.Y+r.H))points.Add(p);}
        var graph=new[]{request.From,request.To}.Concat(points).ToArray();var cost=Enumerable.Repeat(double.PositiveInfinity,graph.Length).ToArray();var done=new bool[graph.Length];cost[0]=0;
        while(true)
        {
            int current=-1;for(int i=0;i<graph.Length;i++)if(!done[i]&&(current<0||cost[i]<cost[current]))current=i;
            if(current<0||double.IsPositiveInfinity(cost[current]))return double.PositiveInfinity;if(current==1)return cost[1];done[current]=true;
            for(int next=0;next<graph.Length;next++){if(done[next]||graph[current].DistanceTo(graph[next])<1e-8)continue;double proposed=cost[current]+graph[current].DistanceTo(graph[next]);if(proposed>=cost[next]||!Segment(graph[current],graph[next],rectangles,.5))continue;cost[next]=proposed;}
        }
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldTerrainDemandTests.Example(true);var s=e.Recipe.PopulationSource!;var source=new WorldMetadataGenerator().Generate(s.Recipe,s.Catalog);
        var route=source.RoutePlan!.Routes.OrderByDescending(r=>r.Forward.Path!.Points.Count).ThenBy(r=>r.ConnectionId,StringComparer.Ordinal).First();
        var link=source.Connections.Single(c=>c.Id==route.ConnectionId);var rule=source.RoutePlan.Rules.Single(r=>r.Id==route.RuleId);
        return(e.Recipe with{AdditionalConnections=[..e.Recipe.AdditionalConnections??[],new("new-journey","new-journey",link.FromId,link.ToId,link.Bidirectional)],
            RouteRules=[..e.Recipe.RouteRules!,rule with{Id="new-journey",ConnectionKind="new-journey",Search=rule.Search with{Objective=WorldRouteObjective.LowestCost}}]},e.Catalog);
    }
    public static async Task RunAsync()
    {
        Geometry();var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==270&&world.AccessPlan!.Sites.Count==269&&world.RoutePlan!.Routes.Count==20,"all physical content plus a new searched journey");
        var added=world.RoutePlan!.Routes.Single(r=>r.RuleId=="new-journey");Check(!world.RoutePlan.PreservedConnectionIds!.Contains(added.ConnectionId)&&added.Forward.Path!.Points.Count>2,"new detour is searched, not copied from source");
        Check(added.Forward.CandidateAudit is {CompleteWinningSet:true,Unexamined:0}&&added.Forward.CandidateAudit.Proposals>world.RoutePlan.Rules.Single(r=>r.Id=="new-journey").Search.MaximumCandidates,"complete winning-set receipt on actual new world journey");
        var tampered=added with{Forward=added.Forward with{CandidateAudit=added.Forward.CandidateAudit! with{Proposals=added.Forward.CandidateAudit.Proposals+1}}};
        var tamperedWorld=world with{RoutePlan=world.RoutePlan with{Routes=world.RoutePlan.Routes.Select(r=>r.ConnectionId==added.ConnectionId?tampered:r).ToArray()}};
        // The complete receipt owns whole-document replay and rejects before the
        // narrower route validator. Exercise both actual boundaries explicitly.
        Check(world.PopulationCoordinationPlan is not null,"complete coordination receipt retained");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tamperedWorld),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        Reject(()=>new MetadataRoutePlanner().ValidateDocument(tamperedWorld),"METADATA_ROUTE_PLAN_MISMATCH");
        var source=world.PlacementPlan!.Definition.SourceWorld!;foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(world.RoutePlan.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"all nineteen source paths exact");
        WorldConnectedGrowthTests.Independent(world,e.Catalog);WorldMetadataGenerator.ValidateDocument(world);
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var changed=e.Recipe with{RouteRules=e.Recipe.RouteRules!.Select(r=>r.Id=="new-journey"?r with{Search=r.Search with{Clearance=r.Search.Clearance+.01}}:r).ToArray()};
        var after=session.Apply(new("clearance","Change new-route candidate clearance","test:clearance",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"incremental new-route search equals clean generation");
        string folder=Path.Combine(Path.GetTempPath(),"physical-routing-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"physical-overlay and candidate-audit history replay");
        Console.WriteLine("PASS: complete 270-node/269-physical-member world retains six compounds and nineteen source routes while searching a twentieth journey through every solid; independent geometry, read/export and source-history session replay.");
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example destination exists");var e=Example();Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
    }
}
