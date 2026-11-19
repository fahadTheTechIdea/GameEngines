using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRouteTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldTraversalProfile Profile()=>new("walking","Connect authored exploration destinations","test:walking",1,.5,
        new Dictionary<string,double>{{"meadow",1}});
    private static WorldSurfaceDefinition Terrain(string name)
    {
        var surface=WorldSurfaceTests.Surface("flat");
        var patch=surface.Patches[0] with{Id="obstacle",Kind="water",Priority=1,Boundary=Box(45,0,10,25)};
        return name switch{
            "flat"=>surface,
            "water"=>surface with{Patches=[surface.Patches[0],patch]},
            "cost"=>surface with{Patches=[surface.Patches[0],patch with{Kind="mud"}]},
            "wall"=>surface with{Exclusions=[new("wall","Impassable barrier","test:wall",Box(45,0,10,100))]},
            "hill"=>surface with{Patches=[surface.Patches[0] with{GradientX=.1}]},
            _=>throw new ArgumentException(name)};
    }
    private static WorldRouteRequest Request()=>new("journey","Reach the second destination","test:journey",new(15,15),new(100,15),
        Profile(),new(.5,[],MaximumWork:40000000));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();
        var root=e.Catalog.Archetypes[0] with{Parts=[new("locations","location",2,"Required exploration destination",
            Layout:MetadataLineLayout.Parameters([10,10],[85,0]))],Connections=[new("links","travel-intent",MetadataConnectionPattern.Chain,"locations")]};
        return(e.Recipe with{Id="terrain-routes",Origin=[5,5],Surface=Terrain("water"),
            RemainingScopes=["Authored terrain and destinations; procedural regions, structures, junctions, artwork and runtime remain open"],
            RouteRules=[new("walk-links","travel-intent",Profile(),Request().Search)]},
            e.Catalog with{Archetypes=[root,e.Catalog.Archetypes[1]]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("EXAMPLE_EXISTS");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var planner=new WorldSurfaceRoutePlanner();var request=Request();
        var flat=planner.Plan(Terrain("flat"),request);
        Check(flat.Status==WorldRouteStatus.Found && flat.Path!.Points.Count==2,"direct route on flat terrain");
        Near(flat.Cost!.Distance,85,"analytic flat distance");
        var water=planner.Plan(Terrain("water"),request);
        var curvedRequest=request with{Search=request.Search with{Objective=WorldRouteObjective.FirstFeasible,Rounding=new(.05,4)}};
        var curved=planner.Plan(Terrain("water"),curvedRequest);
        Console.WriteLine($"Rounded water route: {curved.Status}, {curved.Path?.Points.Count} points, {curved.Work} work; witness {curved.RoundingWitness?.SourcePath.Points.Count} points.");
        Check(curved.Status==WorldRouteStatus.Found && curved.RoundingWitness is not null,"terrain-qualified rounded route");
        var raw=planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{Rounding=null}});
        Check(Json(raw.Path)==Json(curved.RoundingWitness!.SourcePath)&&Json(raw.Cost)==Json(curved.RoundingWitness.SourceCost),"rounding retains original route witness");
        Check(curved.Path!.Points.Count>raw.Path!.Points.Count&&curved.Path.Points[0]==request.From&&curved.Path.Points[^1]==request.To,"rounded polyline preserves endpoints");
        Check(Json(curved)==Json(planner.Plan(Terrain("water"),curvedRequest)),"rounded route deterministic replay");
        var narrowPath=new WorldPolyline([request.From,new(43,26.1),new(57,26.1),request.To]);
        var narrowSource=planner.Compile(Terrain("water"),request,narrowPath);
        Check(narrowSource.Status==WorldRouteStatus.Found,"unrounded narrow detour remains traversable");
        var blocked=WorldSurfaceRoutePlanner.RoundQualified(new(Terrain("water")),curvedRequest with{Search=curvedRequest.Search with{Rounding=new(15,4)}},narrowSource,
            new WorldSpatialWorkBudget(40000000,default),null,default);
        Check(blocked.Status==WorldRouteStatus.TraceRejected&&blocked.Path is null&&blocked.RoundingWitness is not null,"rounding that cuts across forbidden terrain is refused");
        var alternativeRequest=curvedRequest with{Search=curvedRequest.Search with{Rounding=new(15,4,[.05])}};
        var alternativeBudget=new WorldSpatialWorkBudget(40000000,default);
        var recovered=WorldSurfaceRoutePlanner.RoundCandidates(new(Terrain("water")),alternativeRequest,narrowSource,alternativeBudget,null,default);
        Check(recovered.Status==WorldRouteStatus.Found&&recovered.RoundingAttempts is {Count:2}&&
            recovered.RoundingAttempts[0].Status==WorldRouteStatus.TraceRejected&&recovered.RoundingAttempts[1].Status==WorldRouteStatus.Found,
            "declared tighter alternative recovers an obstacle-blocked rounding");
        Check(recovered.RoundingAttempts!.Sum(a=>a.EdgeChecks)==recovered.EdgeChecks-narrowSource.EdgeChecks&&
            recovered.RoundingAttempts!.Sum(a=>a.Work)==recovered.Work,"all attempted edges and work are accounted");
        Reject(()=>WorldSurfaceRoutePlanner.RoundCandidates(new(Terrain("water")),alternativeRequest with{Search=alternativeRequest.Search with{
            MaximumEdgeChecks=recovered.EdgeChecks-1}},narrowSource,new WorldSpatialWorkBudget(40000000,default),null,default),"WORLD_ROUTE_EDGE_BUDGET");
        Reject(()=>WorldSurfaceRoutePlanner.RoundCandidates(new(Terrain("water")),alternativeRequest,narrowSource,
            new WorldSpatialWorkBudget(recovered.Work-1,default),null,default),"WORLD_ROUTE_WORK_BUDGET");
        var allBlocked=WorldSurfaceRoutePlanner.RoundCandidates(new(Terrain("water")),alternativeRequest with{Search=alternativeRequest.Search with{
            Rounding=new(15,4,[10])}},narrowSource,new WorldSpatialWorkBudget(40000000,default),null,default);
        Check(allBlocked.Status==WorldRouteStatus.TraceRejected&&allBlocked.Path is null&&allBlocked.RoundingAttempts is {Count:2},"all blocked authored alternatives reject");
        var protectedWaypoint=new WorldPoint(43,26.1);
        var waypointResult=WorldSurfaceRoutePlanner.RoundQualified(new(Terrain("water")),curvedRequest with{Search=curvedRequest.Search with{Waypoints=[protectedWaypoint]}},narrowSource,
            new WorldSpatialWorkBudget(40000000,default),null,default);
        Check(waypointResult.Status==WorldRouteStatus.Found&&waypointResult.Path!.Points.Contains(protectedWaypoint),"selected authored waypoint is preserved");
        foreach(var (a,b) in curved.Path.Points.Zip(curved.Path.Points.Skip(1)))
            for(int i=0;i<=100;i++)
            {
                double x=a.X+(b.X-a.X)*i/100,y=a.Y+(b.Y-a.Y)*i/100;
                double dx=Math.Max(45-x,Math.Max(0,x-55)),dy=Math.Max(-y,Math.Max(0,y-25));
                Check(Math.Sqrt(dx*dx+dy*dy)>1,"rounded path keeps full width around water");
            }
        Reject(()=>planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{MaximumEdgeChecks=curved.EdgeChecks-1}}),"WORLD_ROUTE_EDGE_BUDGET");
        Reject(()=>planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{MaximumWork=curved.Work-1}}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{Rounding=new(0)}}),"WORLD_ROUTE_ROUNDING_INVALID");
        Reject(()=>planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{Rounding=new(1,4,[.5,.5])}}),"WORLD_ROUTE_ROUNDING_ALTERNATIVES_INVALID");
        Reject(()=>planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{Rounding=new(1,4,[2])}}),"WORLD_ROUTE_ROUNDING_ALTERNATIVES_INVALID");
        Reject(()=>planner.Plan(Terrain("water"),curvedRequest with{Search=curvedRequest.Search with{Objective=WorldRouteObjective.LowestCost}}),"WORLD_ROUTE_ROUNDING_SEARCH_UNSUPPORTED");
        Reject(()=>planner.Compile(Terrain("water"),curvedRequest,raw.Path),"WORLD_ROUTE_ROUNDING_TRACE_UNSUPPORTED");
        var roundedExample=Example();
        var roundedRecipe=roundedExample.Recipe with{RouteRules=[roundedExample.Recipe.RouteRules![0] with{Search=curvedRequest.Search with{Rounding=new(.05,4,[.02])}}]};
        var roundedWorld=new WorldMetadataGenerator().Generate(roundedRecipe,roundedExample.Catalog);
        Check(roundedWorld.RoutePlan!.Routes[0].Forward.RoundingWitness is not null&&roundedWorld.RoutePlan.Routes[0].Reverse!.RoundingWitness is not null,"generated world keeps bidirectional rounded route evidence");
        Check(roundedWorld.RoutePlan.Routes[0].Forward.RoundingAttempts is {Count:1},"generated world retains rounding alternative decision");
        new MetadataRoutePlanner().ValidateDocument(roundedWorld);
        Check(water.Status==WorldRouteStatus.Found && water.Path!.Points.Any(p=>p.Y>26),"same endpoints detour around water");
        Check(Json(water)==Json(planner.Plan(Terrain("water"),request)),"deterministic route including bounded search statistics");
        Check(water.Cost!.Distance>85,"terrain changes actual path length");
        Console.WriteLine($"Detour: {water.CandidateCount} candidates, {water.EdgeChecks} edges, {water.Work} work, {water.Cost.Distance:R} units.");
        // Independent analytic rectangle clearance: dense probes are corroboration, not the planner's feasibility method.
        foreach(var (a,b) in water.Path!.Points.Zip(water.Path.Points.Skip(1)))
            for(int i=0;i<=1000;i++)
            {
                double x=a.X+(b.X-a.X)*i/1000,y=a.Y+(b.Y-a.Y)*i/1000;
                double dx=Math.Max(45-x,Math.Max(0,x-55)),dy=Math.Max(-y,Math.Max(0,y-25));
                Check(Math.Sqrt(dx*dx+dy*dy)>1,"route retains width around water rectangle");
            }
        var expensive=request with{Profile=request.Profile with{KindCosts=new Dictionary<string,double>{{"meadow",1},{"mud",20}}}};
        var cheap=expensive with{Profile=expensive.Profile with{KindCosts=new Dictionary<string,double>{{"meadow",1},{"mud",1}}}};
        var detour=planner.Plan(Terrain("cost"),expensive);var straight=planner.Plan(Terrain("cost"),cheap);
        Check(detour.Status==WorldRouteStatus.Found && detour.Path!.Points.Count>2,"cost can prefer a longer traversable detour");
        Check(detour.Cost!.Total<75+10*20,"detour cost less than analytic direct mud route");
        Check(straight.Status==WorldRouteStatus.Found && straight.Path!.Points.Count==2,"equal-cost terrain permits direct route");
        var reverse=request with{From=request.To,To=request.From,Profile=request.Profile with{AscentCost=3,DescentCost=1}};
        var downhill=planner.Plan(Terrain("hill"),reverse);var uphill=planner.Plan(Terrain("hill"),request with{Profile=reverse.Profile});
        Near(uphill.Cost!.Ascent,25.5,"analytic uphill cost");Near(downhill.Cost!.Descent,8.5,"analytic downhill cost");
        Check(planner.Plan(Terrain("hill"),request with{Profile=request.Profile with{MaximumSlope=.05}}).Status==WorldRouteStatus.EndpointRejected,"slope policy changes feasibility");
        Check(planner.Plan(Terrain("wall"),request).Status==WorldRouteStatus.CandidateGraphDisconnected,"finite graph failure does not claim universal impossibility");

        var baseSurface=Terrain("flat");
        var pocket=baseSurface.Patches[0] with{Id="pocket",Kind="water",Priority=1,Boundary=Box(30,15.7,.01,.01)};
        var pocketSurface=baseSurface with{Patches=[baseSurface.Patches[0],pocket]};
        Check(new WorldSurfaceQuery(pocketSurface).Matches(new([request.From,request.To]),s=>s.Kind=="meadow"),"pocket is not on centerline");
        Check(new WorldSurfaceTraversal(new(pocketSurface),Profile()).Evaluate(request.From,request.To) is null,"full width catches interior pocket missed by centerline");
        var masked=pocketSurface with{Patches=[..pocketSurface.Patches,baseSurface.Patches[0] with{Id="cover",Priority=2,Boundary=Box(29,14,2,3)}]};
        Check(new WorldSurfaceTraversal(new(masked),Profile()).Evaluate(request.From,request.To) is not null,"higher valid field masks forbidden lower field");
        var thin=baseSurface with{Patches=[baseSurface.Patches[0],pocket with{Boundary=Box(50,0,.01,40)}]};
        Check(new WorldSurfaceTraversal(new(thin),Profile()).Evaluate(request.From,request.To) is null,"thin crossing between endpoints rejects");
        var gap=baseSurface with{Patches=[baseSurface.Patches[0] with{Boundary=Box(0,0,49.99,100)},
            baseSurface.Patches[0] with{Id="east",Boundary=Box(50.01,0,70,100),Priority=1}]};
        Check(new WorldSurfaceTraversal(new(gap),Profile()).Evaluate(request.From,request.To) is null,"uncovered sliver rejects");
        var step=baseSurface with{Patches=[baseSurface.Patches[0],pocket with{Kind="meadow",Boundary=Box(45,0,10,40),Elevation=30}]};
        Check(new WorldSurfaceTraversal(new(step),Profile()).Evaluate(request.From,request.To) is null,"centerline discontinuous height rejects");
        var narrow=baseSurface with{Exclusions=[new("lower","Wall","test:wall",Box(45,0,10,14.2)),new("upper","Wall","test:wall",Box(45,15.8,10,100))]};
        Check(new WorldSurfaceTraversal(new(narrow),Profile()).Evaluate(request.From,request.To) is null,"narrow pass cannot admit full diameter");
        var area=new WorldSurfaceAreaQuery(new(pocketSurface));
        Check(!area.Matches(Box(20,14,20,2),s=>s.Kind=="meadow"),"area query sees enclosed tiny island");
        // A disjoint grid of tiny analytic rectangles probes the full envelope, not planner vertices.
        for(int i=0;i<80;i++)
        {
            double x=20+(i%10)*7, y=13+(i/10)*.5;
            var testSurface=baseSurface with{Patches=[baseSurface.Patches[0],pocket with{Boundary=Box(x,y,.01,.01)}]};
            bool accepted=new WorldSurfaceTraversal(new(testSurface),Profile()).Evaluate(request.From,request.To) is not null;
            double distance=y>15?y-15:Math.Max(0,15-y-.01);
            if(distance<1)Check(!accepted,"analytic pocket/capsule intersection cannot be accepted");
            if(distance>1.03)Check(accepted,"well-separated pocket must not reject passage");
        }
        WorldPoint Rotate(WorldPoint p)=>new(p.X*Math.Cos(.37)-p.Y*Math.Sin(.37)-140,p.X*Math.Sin(.37)+p.Y*Math.Cos(.37)-80);
        WorldPolygon RotatePolygon(WorldPolygon p)=>new(p.Vertices.Select(Rotate).ToArray());
        var rotated=Terrain("water") with{Boundary=RotatePolygon(baseSurface.Boundary!),Patches=Terrain("water").Patches.Select(p=>p with{
            Boundary=RotatePolygon(p.Boundary),Origin=Rotate(p.Origin)}).ToArray()};
        var turned=planner.Plan(rotated,request with{From=Rotate(request.From),To=Rotate(request.To)});
        Check(turned.Status==WorldRouteStatus.Found && turned.Path!.Points.Count>2,"rotated irregular negative-coordinate domain routes without axis grid");
        var valley=planner.Plan(WorldSurfaceTests.Surface("valley"),request);
        Check(valley.Status==WorldRouteStatus.Found,"continuous valley fields can share a qualified route");
        Check(planner.Plan(WorldSurfaceTests.Surface("coast"),request).Status==WorldRouteStatus.EndpointRejected,"excluded sea destination is refused");
        Reject(()=>planner.Plan(baseSurface,request with{Search=request.Search with{MaximumWork=1}}),"WORLD_ROUTE_WORK_BUDGET");
        Check(planner.Plan(baseSurface,request with{Search=request.Search with{MaximumCandidates=2}}).CandidateCount==2,"proven optimal direct passage needs only its endpoints");
        Reject(()=>planner.Plan(Terrain("water"),request with{Search=request.Search with{MaximumCandidates=2}}),"WORLD_ROUTE_CANDIDATE_BUDGET");
        Reject(()=>planner.Plan(Terrain("water"),request with{Search=request.Search with{MaximumEdgeChecks=1}}),"WORLD_ROUTE_EDGE_BUDGET");
        Reject(()=>planner.Plan(baseSurface,request with{Profile=Profile() with{Radius=0}}),"WORLD_TRAVERSAL_PROFILE_INVALID");
        Reject(()=>planner.Plan(baseSurface,request with{Search=request.Search with{Waypoints=[new(double.NaN,0)]}}),"WORLD_ROUTE_CANDIDATE_INVALID");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();Cancelled(()=>planner.Plan(baseSurface,request,cancelled.Token));}
        using(var during=new CancellationTokenSource())Cancelled(()=>area.Matches(Box(5,5,2,2),s=>{during.Cancel();return true;},during.Token));

        var example=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(example.Recipe,example.Catalog);
        var routeOwner=new MetadataRoutePlanner();
        var tooMany=Enumerable.Range(0,65).Select(i=>world.Connections[0] with{Id="many-"+i}).ToArray();
        Reject(()=>routeOwner.Plan([example.Recipe.RouteRules![0] with{Search=request.Search with{MaximumWork=1}}],example.Recipe.Surface!,world.Space,world.Nodes,tooMany),"METADATA_ROUTE_TOTAL_BUDGET");
        var highCeiling=routeOwner.Plan([example.Recipe.RouteRules![0] with{Search=request.Search with{MaximumWork=200000000}}],example.Recipe.Surface!,world.Space,world.Nodes,world.Connections);
        Check(highCeiling.Work<=highCeiling.Budget!.MaximumWork,"independent leg ceilings do not reserve work; consumed aggregate stays bounded");
        Check(world.RoutePlan?.Routes.Count==1 && world.RoutePlan.Routes[0].Reverse is not null,"actual metadata realizes declared bidirectional journey");
        Check(generator.Generate(example.Recipe with{RouteRules=null},example.Catalog).RoutePlan is null,"unselected links remain logical");
        Reject(()=>generator.Generate(example.Recipe with{Surface=null},example.Catalog),"METADATA_ROUTE_SURFACE_REQUIRED");
        Reject(()=>generator.Generate(example.Recipe with{Surface=Terrain("wall")},example.Catalog),"METADATA_ROUTE_UNRESOLVED");
        Reject(()=>generator.Generate(example.Recipe with{RouteRules=[example.Recipe.RouteRules![0] with{ConnectionKind="portal"}]},example.Catalog),"METADATA_ROUTE_RULE_UNUSED");
        Reject(()=>generator.Generate(example.Recipe with{RouteRules=[example.Recipe.RouteRules![0],example.Recipe.RouteRules[0] with{Id="duplicate-kind"}]},example.Catalog),"METADATA_ROUTE_RULE_INVALID");
        var route=world.RoutePlan!.Routes[0];
        var forged=world with{RoutePlan=world.RoutePlan with{Routes=[route with{Forward=route.Forward with{Path=new([request.From,request.To])}}]}};
        Reject(()=>new MetadataRoutePlanner().ValidateDocument(forged),"METADATA_ROUTE_PLAN_MISMATCH");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-routes-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);string recipe=Path.Combine(root,"recipe.json"),catalog=Path.Combine(root,"catalog.json"),output=Path.Combine(root,"world");
            await generator.GenerateFilesAsync(recipe,catalog,output);
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"))),"saved geometry rederived on read");
            await File.WriteAllTextAsync(recipe,Json(roundedRecipe));
            string roundedOutput=Path.Combine(root,"rounded-world");
            await generator.GenerateFilesAsync(recipe,catalog,roundedOutput);
            Check(Json(roundedWorld)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(roundedOutput,"world-metadata.json"))),"published rounded world replays exactly");
            await File.WriteAllTextAsync(recipe,Json(example.Recipe));
            string bad=Path.Combine(root,"forged.json");await File.WriteAllTextAsync(bad,Json(forged));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bad),"METADATA_ROUTE_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"cli"))==0,"CLI actual route generation");
                await File.WriteAllTextAsync(recipe,Json(roundedRecipe));
                string cliRounded=Path.Combine(root,"cli-rounded");
                Check(await Run(cli,root,recipe,catalog,cliRounded)==0,"CLI rounded route generation");
                Check(Json(roundedWorld)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(cliRounded,"world-metadata.json"))),"CLI rounded world matches SDK");
                await File.WriteAllTextAsync(recipe,Json(example.Recipe with{Surface=Terrain("wall")}));
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"failed"))==1,"CLI refuses unresolved required route");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"failure cannot publish partial world");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.failed")&&audit.Contains("world.metadata.succeeded"),"durable route outcome audit");
            }
        }
        finally
        {
            if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-routes-",StringComparison.Ordinal))Directory.Delete(root,true);
        }
        Console.WriteLine("PASS: common surface routing; full-width terrain pockets, thin barriers, priority masking, width, cost/grade policies, asymmetric elevation costs, deterministic bounded search, cancellation, metadata/CLI/persistence/tamper/failure audit. No regional layout, junction, vehicle, art or runtime acceptance claim.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Routes: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7,message);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static void Cancelled(Action action){try{action();}catch(OperationCanceledException){return;}throw new Exception("Cancellation absent");}
}
