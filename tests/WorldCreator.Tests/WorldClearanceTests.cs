using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using WorldCreator.Catalogs.Modern;

internal static class WorldClearanceTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldSurfaceDefinition Surface()=>new(1,"flat","meters",Box(0,0,40,30),[],[],HeightField:new(1,"flat","ground","Flat test terrain","test","meters",new(0,0),5,5,9,7,Enumerable.Repeat(0.0,63).ToArray()));
    private static WorldEnvironmentDefinition Fields()=>new(1,"position",[new("x","World x","test","meters",0,[new(WorldFieldInput.X,1)])]);
    private static WorldSuitabilityProfile Profile()=>new("central","Prefer central sites","test",["ground"],[],[new("center","x","Central purpose","test",20,40,1)]);
    private static WorldPlacementRequest Anchored(WorldSiteDemand demand,double x=10,double y=10)=>new("clearance","Place required source","test",Surface().Boundary,[demand],[new("anchor",new(x,y))],[new("position",demand.Id,"anchor","Retain authored anchor","test",0,0,0,1)],MaximumWork:50000000);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldPlacementTests.Example();var profile=Profile();var route=e.Recipe.NetworkRules![0].Options;
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId) with{Parts=[new("sites","site",2,"Required catalog sites")],ChildRequirements=[new("site",2,2)],
            Connections=[new("walk","walk",MetadataConnectionPattern.Chain,"sites")]};
        var site=new WorldMetadataArchetype("site","site","Source footprint with reserved surroundings",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact footprint and placement reservation","catalog:site",Box(-2,-2,4,4),Box(-5,-4,10,8)),
            Access:new(MetadataOccupancyKind.Solid,[new("entry","North entry","catalog:entry",new(0,-2),new(0,-6),1)]));
        var request=new WorldPlacementRequest("sites","Place source sites and clearance","test",Surface().Boundary,
            [new("a","First required site","test",null,profile),new("b","Second required site","test",null,profile)],
            [new("first",new(10,10)),new("second",new(30,20))],
            [new("first","a","first","First source anchor","test",0,0,0,1),new("second","b","second","Second source anchor","test",0,0,0,1)],MaximumWork:50000000);
        return(e.Recipe with{Id="clearance-world",Origin=[20,15],Surface=Surface(),Environment=Fields(),NetworkRules=null,
            RouteRules=[new("walk","walk",route.Profile with{Radius=.3,MaximumSlope=1},route.Search,"entry","entry")],
            Placement=new(request,[new("a","clearance-world/sites-0",true,30),new("b","clearance-world/sites-1",true,-20)]),
            RemainingScopes=["Source footprints and reserved surroundings; directional site grammar, shared services, full siting, junctions, artwork and native runtime remain open."]},e.Catalog with{Archetypes=[root,site]});
    }
    public static async Task WriteExampleAsync(string dir)
    {
        if(Directory.Exists(dir)||File.Exists(dir))throw new IOException("Example exists");Directory.CreateDirectory(dir);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var surface=Surface();var environment=new WorldEnvironmentQuery(new(surface),Fields());var planner=new WorldSitePlacementPlanner();
        var demand=new WorldSiteDemand("a","Required shape","test",Box(-2,-2,4,4),Profile(),Box(-4,-4,8,8));
        var request=Anchored(demand);var result=planner.Plan(environment,request);Check(result.Status==WorldPlacementStatus.Found,"anchored reservation fits");
        Check(result.Sites[0].Footprint.Footprint.Vertices[0]==new WorldPoint(8,8)&&result.Sites[0].Reservation!.Footprint.Vertices[0]==new WorldPoint(6,6),"independent physical and reserved extents");
        Check(planner.Plan(environment,Anchored(demand with{Reservation=null},5,5)).Status==WorldPlacementStatus.Found,"physical boundary control fits at known candidate");
        Check(planner.Plan(environment,Anchored(demand with{Reservation=Box(-6,-6,12,12)},5,5)).Status==WorldPlacementStatus.NoFeasibleAssignment,"reservation outside boundary rejects physical fit");
        Check(planner.Plan(environment,Anchored(demand with{Reservation=Box(-5,-5,10,10)},5,5)).Status==WorldPlacementStatus.Found,"exact reservation boundary fit accepted");
        var hazard=surface with{Exclusions=[new("slit","Thin forbidden feature","test",Box(13,9,.1,2))]};
        var blocked=planner.Plan(new(new(hazard),Fields()),request);
        Check(blocked.Status==WorldPlacementStatus.NoFeasibleAssignment&&blocked.Diagnostics[0].Rejections.Keys.Any(k=>k.StartsWith("reservation:",StringComparison.Ordinal)),"thin hazard outside physical shape blocks reserved area");
        Check(planner.Plan(new(new(hazard),Fields()),request with{Demands=[demand with{Reservation=null}]}).Status==WorldPlacementStatus.Found,"independent physical-only hazard control");
        var constrained=demand with{Profile=Profile() with{Requirements=[new("limit","x","Keep the entire placement on declared land","test",0,12)]}};
        Check(planner.Plan(environment,request with{Demands=[constrained with{Reservation=null}]}).Status==WorldPlacementStatus.Found,"physical scalar-range control passes");
        Check(planner.Plan(environment,request with{Demands=[constrained]}).Status==WorldPlacementStatus.NoFeasibleAssignment,"whole reservation must pass scalar field requirements");
        var pair=request with{Demands=[demand,demand with{Id="b"}],Anchors=[new("anchor",new(10,10)),new("other",new(15,10))],Relations=[request.Relations[0],new("other","b","other","Second anchor","test",0,0,0,1)]};
        Check(planner.Plan(environment,pair).Status==WorldPlacementStatus.NoFeasibleAssignment,"overlapping reservations reject separated physical shapes");
        Check(planner.Plan(environment,pair with{Demands=pair.Demands.Select(d=>d with{Reservation=null}).ToArray()}).Status==WorldPlacementStatus.Found,"physical separation control passes");
        var concave=new WorldPolygon([new(-4,-4),new(4,-4),new(4,4),new(0,4),new(0,2),new(-4,2)]);
        Check(planner.Plan(environment,request with{Demands=[demand with{Footprint=Box(-1,-1,2,2),Reservation=concave}]}).Status==WorldPlacementStatus.Found,"authored concave reservation supported");
        Reject(()=>planner.Plan(environment,request with{Demands=[demand with{Reservation=Box(-1,-1,2,2)}]}),"WORLD_PLACEMENT_RESERVATION_CONTAINMENT");
        Reject(()=>planner.Plan(environment,request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        var gen=new WorldMetadataGenerator();var e=Example();var world=gen.Generate(e.Recipe,e.Catalog);
        foreach(var source in world.PlacementPlan!.Footprints){Check(Near(source.Polygon.Vertices[0].DistanceTo(source.Polygon.Vertices[1]),4),"physical source remains four meters");Check(Near(source.Reservation!.Vertices[0].DistanceTo(source.Reservation.Vertices[1]),10)&&Near(source.Reservation.Vertices[1].DistanceTo(source.Reservation.Vertices[2]),8),"reservation rotates rigidly");}
        Check(world.AccessPlan!.Sites.All(s=>Near(s.Footprint.Vertices[0].DistanceTo(s.Footprint.Vertices[1]),4)),"only physical footprint becomes solid occupancy");
        var capacity=WorldCapacityTests.Example();var capacityCatalog=capacity.Catalog with{Archetypes=capacity.Catalog.Archetypes.Select(a=>a.PhysicalFootprint is {} f?a with{PhysicalFootprint=f with{Reservation=Box(-4,-3,9,7)}}:a).ToArray()};
        var expanded=gen.Generate(capacity.Recipe,capacityCatalog);
        Check(expanded.CapacityPlan!.Allocations[0].Count==3&&expanded.PlacementPlan!.Placement.Sites.All(s=>s.Reservation is not null),"capacity placement templates propagate every source reservation");
        Reject(()=>gen.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select(d=>d with{Reservation=Box(-1,-1,2,2)}).ToArray()}}},e.Catalog),"METADATA_PLACEMENT_FOOTPRINT_SOURCE_CONFLICT");
        var pack=TestCatalogs.Registry.Resolve(ModernCatalog.Selection);var definition=pack.Structures.First(d=>d.SizeVariants?.Any(v=>v.SiteClearanceMeters>0)==true);var variant=definition.SizeVariants!.First(v=>v.SiteClearanceMeters>0);
        var large=new WorldSurfaceDefinition(1,"large","meters",Box(0,0,1000,1000),[],[],HeightField:new(1,"flat","ground","Source test","test","meters",new(0,0),500,500,3,3,Enumerable.Repeat(0.0,9).ToArray()));
        var sourceCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=1}],ChildRequirements=[new("site",1,1)],Connections=[]}:a with{PhysicalFootprint=null,Access=null,Structure=new(ModernCatalog.Selection,definition.Id,variant.Id)}).ToArray()};
        var sourceRequest=Anchored(demand with{Footprint=null,Reservation=null},500,500) with{Boundary=large.Boundary};
        var sourceRecipe=e.Recipe with{Surface=large,Origin=[500,500],RouteRules=null,Placement=new(sourceRequest,[new("a","clearance-world/sites-0",true,37)])};
        var sourceWorld=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(sourceRecipe,sourceCatalog);var resolved=sourceWorld.PlacementPlan!.Footprints.Single();
        Check(Near(resolved.Reservation!.Vertices[0].DistanceTo(resolved.Reservation.Vertices[1]),variant.WidthMeters+2*variant.SiteClearanceMeters)&&Near(resolved.Reservation.Vertices[1].DistanceTo(resolved.Reservation.Vertices[2]),variant.DepthMeters+2*variant.SiteClearanceMeters),"registered clearance exact independent dimension oracle");
        Check(!resolved.RemainingRequirements.Contains("catalog-site-clearance")&&resolved.RemainingRequirements.Contains("catalog-context-and-capacity"),"clearance enforced while full siting stays unresolved");
        Reject(()=>new WorldMetadataGenerator(TestCatalogs.Registry).Generate(sourceRecipe with{Placement=sourceRecipe.Placement! with{Bindings=[new("a","clearance-world/sites-0")],Request=sourceRequest with{Demands=[demand with{Reservation=null}]}}},sourceCatalog),"METADATA_PLACEMENT_CATALOG_FOOTPRINT_REQUIRED");
        CheckParentFeedback();
        var plan=world.PlacementPlan;var tampered=world with{PlacementPlan=plan with{Placement=plan.Placement with{Sites=plan.Placement.Sites.Select(s=>s with{Reservation=null}).ToArray()}}};
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        var changed=world.FootprintDefinitions!.ToDictionary(p=>p.Key,p=>p.Value with{Reservation=Box(-5.1,-4,10.2,8)});
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(world with{FootprintDefinitions=changed}),"METADATA_PLACEMENT_PLAN_MISMATCH");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(environment,request,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-clearance-"+Guid.NewGuid().ToString("N"));
        try{
            await WriteExampleAsync(dir);await gen.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"reservation provenance and geometry replay");
            await File.WriteAllTextAsync(Path.Combine(dir,"bad.json"),Json(tampered));await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"bad.json")),"METADATA_PLACEMENT_PLAN_MISMATCH");
            if(cli is not null){
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"cli"))==0,"actual CLI");
                var impossible=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.PhysicalFootprint is {} f?a with{PhysicalFootprint=f with{Reservation=Box(-100,-100,200,200)}}:a).ToArray()};
                await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(impossible));
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"required clearance failure cannot publish");
                var audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"clearance audit outcomes");
            }
        }finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-clearance-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: physical/reservation oracles, rotation and registered clearance, boundary/hazard/sibling rejection, concavity, child-driven parent feedback, source conflicts, budget/cancellation, replay/tamper and CLI when supplied. Full siting, service delivery, junctions, art and runtime remain open.");
    }
    private static void CheckParentFeedback()
    {
        var surface=new WorldSurfaceDefinition(1,"nested","meters",Box(0,0,20,10),[],[],HeightField:new(1,"flat","ground","Hierarchy test","test","meters",new(0,0),10,10,3,2,[0,0,0,0,0,0]));
        var profile=Profile() with{Preferences=[new("center","x","Preferred parent","test",10,10,1)]};
        var parent=new WorldPlacementRequest("parent","Parent placement","test",surface.Boundary,[new("parent","Parent","test",Box(-4,-4,8,8),profile)],[],[]);
        var child=new MetadataNestedPlacement("child","world/parent",new("child","Child placement","test",null,[new("child","Child","test",Box(-1,-1,2,2),profile with{Requirements=[new("west","x","Child requires western land","test",0,9)]})],[],[]),[new("child","world/parent/child")]);
        var d=new MetadataPlacementDefinition(parent,[new("parent","world/parent")],[child]);
        List<WorldMetadataNode> Nodes()=>[new("world","world","region",null,"Root",new Dictionary<string,JsonElement>(),[10,5],null),new("world/parent","parent","site","world","Parent",new Dictionary<string,JsonElement>(),null,null),new("world/parent/child","child","part","world/parent","Child",new Dictionary<string,JsonElement>(),null,null)];
        var p=new MetadataPlacementPlanner();var baseline=p.Apply(d,Fields(),surface,new(["x","y"],"meters"),Nodes(),[]);
        var reserved=p.Apply(d with{NestedStages=[child with{Request=child.Request with{Demands=[child.Request.Demands[0] with{Reservation=Box(-2,-2,4,4)}]}}]},Fields(),surface,new(["x","y"],"meters"),Nodes(),[]);
        Check(baseline.Placement.Sites.Single().Position.X==10&&reserved.Placement.Sites.Single().Position.X==5,"child reservation, not physical size, forces parent reconsideration");
    }
    private static bool Near(double a,double b)=>Math.Abs(a-b)<1e-7;
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action f,string code){try{f();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> f,string code){try{await f();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task<int> Run(string cli,string dir,params string[] args){var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);using var p=Process.Start(info)!;var o=p.StandardOutput.ReadToEndAsync();var e=p.StandardError.ReadToEndAsync();using var c=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await p.WaitForExitAsync(c.Token);}catch(OperationCanceledException){p.Kill(true);throw;}Console.Write(await o);Console.Write(await e);return p.ExitCode;}
}
