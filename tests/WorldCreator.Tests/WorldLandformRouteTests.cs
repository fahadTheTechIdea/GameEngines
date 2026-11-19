using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLandformRouteTests
{
    internal static (WorldMetadataRecipe Recipe, WorldMetadataCatalog Catalog) Example(bool south)
    {
        var e = WorldCompoundIntegrationTests.Example();
        var boundary = new WorldPolygon([new(0,0),new(56,0),new(56,36),new(52,40),new(4,40),new(0,36)]);
        double pass = south ? 28 : 12;
        var terrain = new WorldTerrainRecipe(1,"ridge","ground","Ridge with an open mountain pass","landform:ridge","meters",
            new(0,0),2,2,29,21,0,0,0,
            [new("north-ridge","Northern ridge arm","landform:ridge",WorldLandformMode.Add,[new(28,0),new(28,pass-6)],4,8),
             new("south-ridge","Southern ridge arm","landform:ridge",WorldLandformMode.Add,[new(28,pass+6),new(28,40)],4,8)]);
        var placement = e.Recipe.Placement!;
        return (e.Recipe with {
            Surface = new(1,"ridge-world","meters",boundary,[],[],terrain),
            Regions = null, LocalLayouts = null, LayoutAdaptation = null,
            Placement = placement with { Request = placement.Request with {
                Boundary = boundary, Anchors = [new("a",new(10,20)),new("b",new(46,20))],
                Demands = placement.Request.Demands.Select(d=>d with {RegionId=null,AllocationId=null}).ToArray()}},
            RouteRules = e.Recipe.RouteRules!.Select(r=>r with {Interfaces=null,
                Profile=r.Profile with {MaximumSlope=.2},Search=r.Search with {MaximumWork=30000000,MaximumCandidates=512}}).ToArray(),
            RemainingScopes=["Two complete fixed outposts across a generated ridge. Pass relocation is full regeneration; incremental invalidation, broader world diversity, art and native runtime remain open."]
        },e.Catalog);
    }

    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(bool south in new[]{false,true})
        {
            var e=Example(south);string folder=Path.Combine(directory,south?"south-pass":"north-pass");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }

    public static async Task RunAsync()
    {
        DirectCases();
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(bool south in new[]{false,true})
        {
            var e=Example(south);
            Check(e.Recipe.RouteRules!.All(r=>r.Search.Waypoints.Count==0),"no pass waypoints supplied");
            var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            Check(world.Nodes.Count==9&&world.AccessPlan!.Sites.Count==8,"all physical compound members retained");
            Check(world.PlacementPlan!.RelativePositions.Count==0,"no marker-only members");
            var trail=world.RoutePlan!.Routes.Single(r=>r.Forward.Id.Contains("trail",StringComparison.Ordinal));
            Check(trail.Forward.Path!.Points.Count>2,"generated ridge requires a detour");
            // Every valid crossing of x=28 must lie in the four-meter flat gap.
            foreach(var route in new[]{trail.Forward,trail.Reverse!})
            foreach(var (a,b) in route.Path!.Points.Zip(route.Path.Points.Skip(1)))
            {
                if((a.X<28&&b.X>=28)||(a.X>28&&b.X<=28))
                {
                    double y=a.Y+(b.Y-a.Y)*(28-a.X)/(b.X-a.X);
                    Check(Math.Abs(y-(south?28:12))<2,"independent ridge-crossing interval");
                }
            }
            Console.WriteLine($"Generated {(south?"south":"north")} pass: {world.Nodes.Count} nodes, {world.AccessPlan!.Sites.Count} physical members, {trail.Forward.CandidateCount} candidates, {trail.Forward.Work} route work, path={Json(trail.Forward.Path)}");
        }
        Check(Json(worlds[0].Nodes)==Json(worlds[1].Nodes)&&Json(worlds[0].AccessPlan!.Sites)==Json(worlds[1].AccessPlan!.Sites),"protected anchors/source geometry unchanged");
        Check(Json(Example(false).Recipe with{Surface=Example(true).Recipe.Surface})==Json(Example(true).Recipe),"only landform input changes");
        var southSurface=new MetadataPhysicalAccessPlanner().RouteSurface(worlds[1].Surface!,worlds[1].AccessPlan!);
        var traversal=new WorldSurfaceTraversal(southSurface,Example(true).Recipe.RouteRules![0].Profile);
        var old=worlds[0].RoutePlan!.Routes.Single(r=>r.Forward.Id.Contains("trail",StringComparison.Ordinal)).Forward.Path!.Points;
        Check(old.Zip(old.Skip(1)).Any(pair=>traversal.Evaluate(pair.First,pair.Second) is null),"old route invalid after pass moves");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-landform-routes-"+Guid.NewGuid().ToString("N"));
        await WriteExamplesAsync(temp);
        foreach(var (name,index) in new[]{("north-pass",0),("south-pass",1)})
        {
            string folder=Path.Combine(temp,name);
            await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"output"));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"output/world-metadata.json")))==Json(worlds[index]),"deterministic saved replay");
        }
        Console.WriteLine("PASS: generated pass relocation, no supplied route waypoints, unchanged complete physical compounds, stale route rejection and deterministic file replay.");
    }
    public static void DirectCases()
    {
        var e=Example(false);var surface=new WorldSurfaceQuery(e.Recipe.Surface!);
        var planner=new WorldSurfaceRoutePlanner();var rule=e.Recipe.RouteRules![0];
        var request=new WorldRouteRequest("pass","Connect fixed outposts","landform:test",new(13,17.5),new(49,17.5),rule.Profile,rule.Search);
        var route=planner.Plan(surface,request);
        Check(route.Status==WorldRouteStatus.Found&&route.SearchModel=="terrain-feature-visibility-v1","terrain search representation declared");
        var imported=surface.Definition with{Terrain=null};
        Check(Json(planner.Plan(imported,request))==Json(route),"imported and generated identical fields have identical search");
        var closed=e.Recipe.Surface! with{Terrain=e.Recipe.Surface!.Terrain! with{Operations=[new("ridge","Unbroken barrier","test:closed",WorldLandformMode.Add,[new(28,0),new(28,40)],4,8)]}};
        Check(planner.Plan(closed,request).Status==WorldRouteStatus.CandidateGraphDisconnected,"unbroken ridge refuses route");
        Check(planner.Plan(surface,request with{Profile=request.Profile with{Radius=2.1}}).Status==WorldRouteStatus.CandidateGraphDisconnected,"centerline gap cannot accept oversized full-width passage");
        Reject(()=>planner.Plan(surface,request with{Search=request.Search with{MaximumCandidates=2}}),"WORLD_ROUTE_CANDIDATE_BUDGET");
        Reject(()=>planner.Plan(surface,request with{Search=request.Search with{MaximumWork=1}}),"WORLD_ROUTE_WORK_BUDGET");
        using var c=new CancellationTokenSource();c.Cancel();
        try{planner.Plan(surface,request,c.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: generated/imported search equivalence, unbroken/too-narrow ridge refusal, explicit candidate/work limits and cancellation.");
    }
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Landform routes: "+message);}
}
