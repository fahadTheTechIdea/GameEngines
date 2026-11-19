using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRouteJunctionTests
{
    internal static void SourceHeights()
    {
        var definition=Surface() with{Patches=[Surface().Patches[0] with{Elevation=.37,Origin=new(2.6,4.2),GradientX=.025,GradientY=.01}]};
        var field=new WorldHeightField(1,"height-source","land","Continuous source field","junction:field","meters",new(0,0),40,10,3,3,
            Enumerable.Range(0,9).Select(i=>.025*(i%3)*40+.01*(i/3)*10).ToArray());
        foreach(var source in new[]{definition,definition with{Patches=[],HeightField=field}})
        {
            var query=new WorldSurfaceQuery(source);var traversal=new WorldSurfaceTraversal(query,Request().Profile);
            var point=new WorldPoint(29.6,11.3);var budget=new WorldSpatialWorkBudget(2000000,default);
            var approaches=new[]{new WorldPoint(2,3),new(77,18),new(15,17),new(35,2),new(48,7),new(61,13)};
            var heights=approaches.Select(a=>traversal.ApproachElevation(point,a,budget,default)).ToArray();
            Check(heights.Distinct().Count()==1&&heights[0]==query.Sample(point).Elevation,
                "one source at one vertex must have one exact height, independent of the incoming segment: "+Json(heights));
            foreach(var a in approaches)
            {
                var edge=traversal.EvaluateEdge(a,point,budget,default);
                Check(edge is {} qualified&&qualified.ToHeight==heights[0]&&qualified.FromHeight==query.Sample(a).Elevation,
                    "qualified directed edges retain their exact source endpoint heights");
            }
        }
        Console.WriteLine("PASS: affine and continuous sampled sources produce one canonical vertex height for every arrival, without rounding or merging distinct source levels.");
    }
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Route junction: "+reason);}
    static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    static void Near(double a,double b,string reason)=>Check(Math.Abs(a-b)<1e-7,reason+": "+a+" vs "+b);
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static WorldSurfaceDefinition Surface()=>new(1,"height-bank","meters",Box(0,0,80,20),[],[
        new("base","land","Low ground","junction:base",Box(0,0,80,20),0,new(0,0),0,0,0),
        new("bank","land","High ground with an abrupt edge","junction:bank",Box(40,0,40,20),1,new(40,0),20,0,0)]);
    static WorldRouteRequest Request()=>new("cross-bank","Cross only through an actual qualified connection","junction:route",new(10,10),new(70,10),
        new("walk","Bounded step traversal","junction:walk",.25,2.1,new Dictionary<string,double>{{"land",1}},AscentCost:1,MaximumHeightStep:.01),
        new(.2,[new(40,10)],MaximumWork:20000000));
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new WorldSurfaceRoutePlanner();var surface=Surface();var request=Request();
        var result=planner.Plan(surface,request);
        WorldRouteResult? compiled=result.Path is {} path?planner.Compile(surface,request,path):null;
        await File.WriteAllTextAsync(Path.Combine(output,"barrier.json"),Json(new{Surface=surface,Request=request,Result=result,Compiled=compiled}));
        Check(result.Status!=WorldRouteStatus.Found,"a waypoint cannot bypass an impassable height step; searched="+result.Status+", compiled="+compiled?.Status);
        foreach(double shift in new[]{-1e-7,-1e-9,1e-9,1e-7})
            Check(planner.Plan(surface,request with{Search=request.Search with{Waypoints=[new(40+shift,10)]}}).Status!=WorldRouteStatus.Found,
                "near-boundary source classification cannot erase a cliff transition");
        var via=new WorldPoint(40,10);var a=new WorldPoint(29,16);var b=new WorldPoint(41,16);
        var ramp=surface with{Patches=[..surface.Patches,new("ramp","land","Authored traversable grade","junction:ramp",Box(30,14,10,4),2,new(30,14),0,2,0)]};
        var pairs=new HashSet<(WorldPoint,WorldPoint)>();
        foreach(var pair in new[]{(request.From,via),(request.From,a),(a,b),(b,via),(via,request.To)}){pairs.Add(pair);pairs.Add((pair.Item2,pair.Item1));}
        var allowed=new HashSet<WorldPoint>{request.From,request.To,via,a,b};
        var constraint=new WorldRouteConstraint([], (p,_)=>allowed.Contains(p),(x,y,_,_)=>pairs.Contains((x,y)));
        var constrained=request with{Search=request.Search with{Waypoints=[via,a,b],MaximumCandidates=32}};
        var valid=planner.Plan(new WorldSurfaceQuery(ramp),constrained,default,null,constraint);
        Check(valid.Status==WorldRouteStatus.Found&&valid.Path!.Points.SequenceEqual(new[]{request.From,a,b,via,request.To}),"a later high arrival at the same vertex survives the cheaper unusable low arrival");
        var verified=planner.Compile(ramp,constrained,valid.Path!);
        Check(verified.Status==WorldRouteStatus.Found,"the generated alternative satisfies the prescribed-path junction owner");
        double length=request.From.DistanceTo(a)+a.DistanceTo(b)+b.DistanceTo(via)+via.DistanceTo(request.To);
        Near(valid.Cost!.Distance,length,"independent ramp path length");Near(valid.Cost.Ascent,20,"independent ramp ascent cost");Near(valid.Cost.Total,length+20,"independent complete route cost");
        Near(verified.Cost!.Total,valid.Cost.Total,"searched/prescribed cost parity");
        Check(Json(planner.Plan(new WorldSurfaceQuery(ramp),constrained with{Search=constrained.Search with{Waypoints=[b,a,via]}},default,null,constraint))==Json(valid),"waypoint order preserves the complete result");
        Check(Json(planner.Plan(new WorldSurfaceQuery(ramp),constrained with{Search=constrained.Search with{MaximumWork=valid.Work}},default,null,constraint))==Json(valid),"exact charged work budget succeeds");
        Reject(()=>planner.Plan(new WorldSurfaceQuery(ramp),constrained with{Search=constrained.Search with{MaximumWork=valid.Work-1}},default,null,constraint),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(new WorldSurfaceQuery(ramp),constrained with{Search=constrained.Search with{MaximumEdgeChecks=valid.EdgeChecks-1}},default,null,constraint),"WORLD_ROUTE_EDGE_BUDGET");
        var reverse=constrained with{From=request.To,To=request.From,Profile=request.Profile with{DescentCost=2}};
        var back=planner.Plan(new WorldSurfaceQuery(ramp),reverse,default,null,constraint);
        Check(back.Status==WorldRouteStatus.Found&&back.Path!.Points.SequenceEqual(valid.Path!.Points.Reverse()),"directed reverse uses the actual ramp");
        Near(back.Cost!.Descent,40,"reverse downhill cost");Near(back.Cost.Total,length+40,"reverse analytic total");
        var small=surface with{Patches=surface.Patches.Select(p=>p.Id=="bank"?p with{Elevation=.005}:p).ToArray()};
        var straight=planner.Compile(small,request,new([request.From,request.To]));
        var split=planner.Compile(small,request,new([request.From,via,request.To]));
        Check(straight.Status==WorldRouteStatus.Found&&split.Status==WorldRouteStatus.Found,"small permitted step remains traversable");
        Near(straight.Cost!.Ascent,.005,"single-edge step cost");Near(split.Cost!.Ascent,.005,"splitting a path cannot erase climb cost");
        Near(straight.Cost.Total,split.Cost.Total,"vertex insertion preserves actual terrain cost");
        var zeroStep=request with{Profile=request.Profile with{MaximumHeightStep=0}};
        var tiny=surface with{Patches=surface.Patches.Select(p=>p.Id=="bank"?p with{Elevation=1e-9}:p).ToArray()};
        var tinyDirect=planner.Compile(tiny,zeroStep,new([request.From,request.To]));var tinySplit=planner.Compile(tiny,zeroStep,new([request.From,via,request.To]));
        Check(tinyDirect.Status==WorldRouteStatus.Found&&tinySplit.Status==WorldRouteStatus.Found&&tinyDirect.Cost!.Ascent==1e-9&&tinySplit.Cost!.Ascent==1e-9,
            "segment interiors and vertices share the existing 1e-8 height tolerance without erasing tiny climb costs");
        var aboveTolerance=tiny with{Patches=tiny.Patches.Select(p=>p.Id=="bank"?p with{Elevation=2e-8}:p).ToArray()};
        Check(planner.Compile(aboveTolerance,zeroStep,new([request.From,request.To])).Status==WorldRouteStatus.TraceRejected&&
            planner.Compile(aboveTolerance,zeroStep,new([request.From,via,request.To])).Status==WorldRouteStatus.TraceRejected,"splitting cannot change a step rejection above the shared tolerance");
        var searchedSmall=planner.Plan(small,request);Near(searchedSmall.Cost!.Total,straight.Cost.Total,"search cannot prefer a free vertex climb");
        foreach(var objective in new[]{WorldRouteObjective.FirstFeasible,WorldRouteObjective.LowestCost})
        {
            var selected=planner.Plan(ramp,request with{Search=request.Search with{Objective=objective}});
            Check(selected.Status==WorldRouteStatus.Found&&planner.Compile(ramp,request,selected.Path!).Status==WorldRouteStatus.Found,"generated free-search route always compiles for "+objective);
        }
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(ramp,request,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"ramp.json"),Json(new{Surface=ramp,Request=constrained,Result=valid,Compiled=verified,ReverseRequest=reverse,Reverse=back}));
        await File.WriteAllTextAsync(Path.Combine(output,"step-cost.json"),Json(new{Surface=small,Request=request,Straight=straight,Split=split,Searched=searchedSmall}));
        var original=WorldSurfaceTests.Example();var root=original.Catalog.Archetypes[0] with{
            Parts=[new("locations","location",2,"Mandatory destinations",Layout:MetadataLineLayout.Parameters([0,0],[60,0]))],
            Connections=[new("join","travel",MetadataConnectionPattern.Chain,"locations")]};
        var catalog=original.Catalog with{Archetypes=[root,original.Catalog.Archetypes[1]]};
        var recipe=original.Recipe with{Id="height-aware-world",Origin=[10,10],Surface=ramp,RouteRules=[new("walk","travel",request.Profile,request.Search)]};
        var generator=new WorldMetadataGenerator();
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(catalog));
        var world=await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
        Check(world.Nodes.Count==3&&world.RoutePlan!.Routes.Count==1&&world.RoutePlan.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"complete authored metadata content and both required route directions remain");
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"current source world replays");
        var blocked=recipe with{Surface=surface};await File.WriteAllTextAsync(Path.Combine(output,"blocked-recipe.json"),Json(blocked));
        try{await generator.GenerateFilesAsync(Path.Combine(output,"blocked-recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"blocked-world"));throw new Exception("Blocked route published");}
        catch(MetadataSpatialConstraintException error)when(error.Message.StartsWith("METADATA_ROUTE_UNRESOLVED",StringComparison.Ordinal)){}
        Check(!Directory.Exists(Path.Combine(output,"blocked-world")),"an impossible required route publishes no world");
        Console.WriteLine("PASS: no false cliff crossing; alternate arrival states preserve a valid ramp, analytic climb/descent and split-path costs, both search policies, exact work/edge bounds, cancellation and full metadata publication/replay/refusal.");
    }
}
